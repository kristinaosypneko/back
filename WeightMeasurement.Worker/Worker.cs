using RabbitMQ.Client; // <--- Основная директива для IModel, IConnection, ConnectionFactory
using RabbitMQ.Client.Events; // <--- Для EventingBasicConsumer, AsyncEventingBasicConsumer, BasicDeliverEventArgs
using System.Text; // Для Encoding
using System.Text.Json; // Для JsonSerializer
using Microsoft.Extensions.Hosting; // Для BackgroundService
using Microsoft.Extensions.Logging; // Для ILogger
using Microsoft.Extensions.Configuration; // Для IConfiguration
using Microsoft.Extensions.DependencyInjection; // Для IServiceProvider (для создания scope)
using WeightApiService.Core.Interfaces; // Для IMeasurementService
using WeightApiService.Core.Models.DTOs; // Для MeasurementDTO

namespace WeightMeasurement.Worker;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IServiceProvider _serviceProvider; // Для получения scoped сервисов
    private readonly IConfiguration _configuration;
    private IConnection? _connection;
    private IModel? _channel;
    private string? _queueName;

    public Worker(ILogger<Worker> logger, IServiceProvider serviceProvider, IConfiguration configuration)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _configuration = configuration;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Worker Service запускается.");
        try
        {
            InitializeRabbitMQ();
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Не удалось инициализировать RabbitMQ. Worker не будет запущен.");
            // Можно остановить приложение, если RabbitMQ критичен для запуска
            // Environment.Exit(1); // или другой способ graceful shutdown
            return Task.FromException(ex); // Завершаем задачу с ошибкой
        }
        return base.StartAsync(cancellationToken);
    }

    private void InitializeRabbitMQ()
    {
        var rabbitMQConfig = _configuration.GetSection("RabbitMQ");
        var hostName = rabbitMQConfig["HostName"];
        var userName = rabbitMQConfig["UserName"];
        var password = rabbitMQConfig["Password"];
        _queueName = rabbitMQConfig["QueueName"];

        if (string.IsNullOrEmpty(hostName) || string.IsNullOrEmpty(_queueName))
        {
            _logger.LogCritical("Конфигурация RabbitMQ (HostName или QueueName) не найдена.");
            throw new InvalidOperationException("Конфигурация RabbitMQ неполная.");
        }

        var factory = new ConnectionFactory()
        {
            HostName = hostName,
            UserName = userName,
            Password = password,
            DispatchConsumersAsync = true // Для асинхронной обработки сообщений в consumer.Received
        };

        try
        {
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            _channel.QueueDeclare(queue: _queueName,
                                 durable: true,  // Должно совпадать с объявлением в боте
                                 exclusive: false,
                                 autoDelete: false,
                                 arguments: null);

            // Позволяет воркеру брать только одно сообщение за раз, пока оно не будет подтверждено (Ack/Nack)
            _channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

            _logger.LogInformation("Worker подключен к RabbitMQ ({HostName}), ожидает сообщения в очереди '{QueueName}'.", hostName, _queueName);
        }
        catch (RabbitMQ.Client.Exceptions.BrokerUnreachableException e)
        {
            _logger.LogError(e, "Не удалось подключиться к RabbitMQ хосту {Host}. Убедитесь, что RabbitMQ запущен и доступен.", hostName);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Произошла ошибка при инициализации RabbitMQ.");
            throw;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_channel == null || _queueName == null)
        {
            _logger.LogError("RabbitMQ канал или имя очереди не инициализированы. ExecuteAsync не может быть выполнен.");
            return; // Не можем работать без канала
        }

        stoppingToken.Register(() =>
            _logger.LogInformation("Worker Service останавливается."));

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var messageString = Encoding.UTF8.GetString(body);
            _logger.LogInformation("Получено сообщение: SequenceNumber={SequenceNumber}, Content='{Message}'", ea.DeliveryTag, messageString);

            MeasurementDTO? measurementDto = null;
            try
            {
                var deserializeOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                measurementDto = JsonSerializer.Deserialize<MeasurementDTO>(messageString, deserializeOptions);

                if (measurementDto == null || string.IsNullOrEmpty(measurementDto.TgId) || measurementDto.Weight <= 0)
                {
                    _logger.LogWarning("Некорректные данные в сообщении или оно не может быть десериализовано: {Message}. Отклоняем.", messageString);
                    _channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false); // Некорректное сообщение
                    return; // Прерываем обработку этого сообщения
                }

                // Создаем scope для получения scoped сервисов
                using (var scope = _serviceProvider.CreateScope())
                {
                    var measurementService = scope.ServiceProvider.GetRequiredService<IMeasurementService>();
                    var result = await measurementService.AddByTgIdAsync(measurementDto);

                    if (result.IsSuccess)
                    {
                        _logger.LogInformation("Измерение для TgId {TgId} с весом {Weight} успешно обработано.", measurementDto.TgId, measurementDto.Weight);
                        _channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false); // Подтверждаем успешную обработку
                    }
                    else
                    {
                        _logger.LogError("Не удалось обработать измерение для TgId {TgId}. Ошибки: {Errors}. Сообщение будет отклонено (Nack).",
                            measurementDto.TgId, string.Join(", ", result.Errors.Select(e => e.Message)));
                        // Requeue: false - чтобы избежать бесконечного цикла при постоянных ошибках.
                        // Рассмотрите Dead Letter Exchange (DLX) для таких сообщений.
                        _channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false);
                    }
                }
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "Ошибка десериализации JSON: {Message}. Сообщение будет отклонено (Nack).", messageString);
                _channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Критическая ошибка при обработке сообщения: {Message}. Сообщение будет отклонено (Nack). TgId: {TgId}",
                    messageString, measurementDto?.TgId ?? "N/A");
                // Для неизвестных ошибок также лучше не ставить requeue: true без стратегии DLX
                _channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false);
            }
        };

        // Начинаем потреблять сообщения из очереди
        _channel.BasicConsume(queue: _queueName,
                             autoAck: false, // Важно: ручное подтверждение
                             consumer: consumer);

        _logger.LogInformation("Потребитель (consumer) запущен и слушает очередь '{_queueName}'.", _queueName);

        // Ожидаем, пока не будет запрошена остановка
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Worker Service останавливается (StopAsync).");
        _channel?.Close();
        _connection?.Close();
        _channel?.Dispose();
        _connection?.Dispose();
        await base.StopAsync(cancellationToken);
    }

    // Можно также переопределить Dispose для дополнительной очистки, если потребуется.
    // public override void Dispose()
    // {
    //     _logger.LogInformation("Worker Service освобождает ресурсы (Dispose).");
    //     _channel?.Dispose();
    //     _connection?.Dispose();
    //     base.Dispose();
    // }
}