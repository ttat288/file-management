using FileManagement.Api.Realtime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FileManagement.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EventsController : ControllerBase
    {
        private readonly EventBus _bus;
        private readonly ILogger<EventsController> _logger;

        public EventsController(EventBus bus, ILogger<EventsController> logger)
        {
            _bus = bus;
            _logger = logger;
        }

        [Authorize]
        [HttpGet("stream")]
        public async Task Stream(CancellationToken ct)
        {
            Response.Headers.Append("Content-Type", "text/event-stream");
            Response.Headers.Append("Cache-Control", "no-cache");
            Response.Headers.Append("Connection", "keep-alive");
            Response.Headers.Append("X-Accel-Buffering", "no"); // nginx

            var reader = _bus.Subscribe(out var id);
            try
            {
                // Initial ping
                await Response.WriteAsync($"data: {{\"type\":\"connected\",\"at\":\"{DateTime.UtcNow:o}\"}}\n\n", ct);
                await Response.Body.FlushAsync(ct);

                await foreach (var msg in reader.ReadAllAsync(ct))
                {
                    await Response.WriteAsync($"data: {msg}\n\n", ct);
                    await Response.Body.FlushAsync(ct);
                }
            }
            catch (OperationCanceledException)
            {
                // client disconnected
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"SSE stream error: {ex.Message}");
            }
            finally
            {
                _bus.Unsubscribe(id);
            }
        }
    }
}

