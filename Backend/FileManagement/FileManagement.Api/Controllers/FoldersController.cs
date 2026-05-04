using FileManagement.Api.Auth;
using FileManagement.Api.Realtime;
using FileManagement.Core.Interfaces;
using FileManagement.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FileManagement.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class FoldersController : ControllerBase
    {
        private readonly IFolderService _folders;
        private readonly EventBus _events;
        private readonly ILogger<FoldersController> _logger;

        public FoldersController(IFolderService folders, EventBus events, ILogger<FoldersController> logger)
        {
            _folders = folders;
            _events = events;
            _logger = logger;
        }

        public record CreateFolderRequest(string Name, Guid? ParentId);
        public record RenameFolderRequest(string NewName);

        [HttpGet]
        public async Task<ActionResult<ApiResponse<PagedResult<FolderDto>>>> GetFolders([FromQuery] Guid? parentId = null, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 200)
        {
            var ownerId = UserContext.GetUserId(User);
            var response = await _folders.GetFoldersAsync(ownerId, parentId, pageNumber, pageSize);
            if (!response.Success) return BadRequest(response);
            return Ok(response);
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponse<FolderDto>>> CreateFolder([FromBody] CreateFolderRequest req)
        {
            var ownerId = UserContext.GetUserId(User);
            var response = await _folders.CreateFolderAsync(ownerId, req.Name, req.ParentId);
            if (!response.Success) return BadRequest(response);
            _events.Publish(new { type = "folder_created", at = DateTime.UtcNow.ToString("o") });
            return Ok(response);
        }

        [HttpPut("{id:guid}/rename")]
        public async Task<ActionResult<ApiResponse<FolderDto>>> RenameFolder(Guid id, [FromBody] RenameFolderRequest req)
        {
            var ownerId = UserContext.GetUserId(User);
            var response = await _folders.RenameFolderAsync(ownerId, id, req.NewName);
            if (!response.Success) return BadRequest(response);
            _events.Publish(new { type = "folder_renamed", at = DateTime.UtcNow.ToString("o") });
            return Ok(response);
        }

        [HttpDelete("{id:guid}")]
        public async Task<ActionResult<ApiResponse>> DeleteFolder(Guid id)
        {
            var ownerId = UserContext.GetUserId(User);
            var response = await _folders.DeleteFolderAsync(ownerId, id);
            if (!response.Success) return BadRequest(response);
            _events.Publish(new { type = "folder_deleted", at = DateTime.UtcNow.ToString("o") });
            return Ok(response);
        }
    }
}
