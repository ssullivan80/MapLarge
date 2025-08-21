using Microsoft.AspNetCore.Mvc;
using TestProject.Models;

namespace TestProject.Controllers {
    [ApiController]
    [Route("[controller]")]
    public class TestController : ControllerBase {
        private const string FileFolder = "File folder";
        private readonly ILogger<TestController> _logger;
        private readonly IConfiguration _configuration;

        public TestController(ILogger<TestController> logger, IConfiguration configuration) {
            _logger = logger;
            _configuration = configuration;
        }

        /// <summary>
        /// Get files and directories in the specified path, optionally filtering by search term and recursion
        /// </summary>
        /// <param name="path"></param>
        /// <param name="search">The search parameters to search for</param>
        /// <param name="recursive">To include subdirectories</param>
        /// <returns></returns>
        [HttpGet("/search")]
        public IActionResult Get([FromQuery] string? path, [FromQuery] string? search, [FromQuery] bool recursive = false) 
        {
            // use default path from configuration if not provided
            if (string.IsNullOrEmpty(path))
            { 
                path = _configuration["ApiSettings:BaseDirectory"];
            }

            // check if path is valid            
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                return BadRequest("Invalid or missing directory path.");
            }

            SearchOption searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

            var files = Directory.GetFiles(path, "*", searchOption).ToList();
            var directories = Directory.GetDirectories(path, "*", searchOption).ToList();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchLower = search.ToLowerInvariant();
                files = files.Where(f => Path.GetFileName(f).ToLowerInvariant().Contains(searchLower)).ToList();
                directories = directories.Where(d => Path.GetFileName(d).ToLowerInvariant().Contains(searchLower)).ToList();
            }

            var fileDetails = files.Select(f => 
            {
                var info = new FileInfo(f);
                return new Result
                {
                    Name = info.Name,
                    Folder = info.DirectoryName,
                    Size = info.Length,                    
                    LastModified = info.LastWriteTime,
                    FullPath = f
                };
            }).ToList();            

            var directoryDetails = directories.Select(d =>
            {
                var directory = new DirectoryInfo(d);
                return new Result
                {
                    Name = directory.Name,
                    Folder = directory.Parent?.FullName ?? string.Empty,
                    Type = FileFolder,
                    LastModified = directory.LastWriteTime,
                    FullPath = d
                };
            }).ToList();

            return Ok(new
            {
                Results = directoryDetails.Concat(fileDetails),
                FileCount = fileDetails.Count,
                DirectoryCount = directoryDetails.Count,                
            });
        }

        /// <summary>
        /// Download a file from the specified path
        /// </summary>
        /// <param name="path">The path of the file to download</param>
        /// <returns></returns>
        [HttpGet("/download")]
        public IActionResult Download([FromQuery] string path)
        {
            // use default path from configuration if not provided
            if (string.IsNullOrEmpty(path))
            {
                path = _configuration["ApiSettings:BaseDirectory"];
            }

            if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
                return BadRequest("Invalid file path.");

            var fileName = Path.GetFileName(path);
            var mimeType = "application/octet-stream";
            var fileBytes = System.IO.File.ReadAllBytes(path);
            return File(fileBytes, mimeType, fileName);
        }

        /// <summary>
        /// Upload a file to the specified path
        /// </summary>
        /// <param name="file">The file to upload</param>
        /// <param name="path">The path to upload to</param>
        /// <returns></returns>
        [HttpPost("/upload")]
        public IActionResult Upload(IFormFile file, [FromForm] string? path)
        {
            // use default path from configuration if not provided
            if (string.IsNullOrEmpty(path))
            {
                path = _configuration["ApiSettings:BaseDirectory"];
            }

            if (file == null || string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                return BadRequest("Invalid upload request.");

            var filePath = Path.Combine(path, Path.GetFileName(file.FileName));
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                file.CopyTo(stream);
            }
            return Ok();
        }

        /// <summary>
        /// Delete a file at the specified path.       
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        [HttpDelete("/delete")]
        public IActionResult Delete([FromForm] string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
                return BadRequest("Invalid file path.");

            try
            {
                System.IO.File.Delete(path);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting file: {Path}", path);
                return StatusCode(500, "Error deleting file.");
            }
        }

        /// <summary>
        /// Move a file from sourcePath to destPath.
        /// </summary>
        /// <param name="sourcePath">source file</param>
        /// <param name="destPath">destination path</param>
        /// <returns></returns>
        [HttpPost("/move")]
        public IActionResult Move([FromForm] string sourcePath, [FromForm] string destPath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || !System.IO.File.Exists(sourcePath))
                return BadRequest("Invalid source file path.");
            if (string.IsNullOrWhiteSpace(destPath))
                return BadRequest("Invalid destination path.");

            //if destination path is a directory, combine with source file name 
            if (Directory.Exists(destPath))
            {
                var fileName = Path.GetFileName(sourcePath);
                destPath = Path.Combine(destPath, fileName);
            }

            try
            {
                System.IO.File.Move(sourcePath, destPath, overwrite: true);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error moving file: {Source} -> {Dest}", sourcePath, destPath);
                return StatusCode(500, "Error moving file.");
            }
        }

        /// <summary>
        /// Copy a file from sourcePath to destPath.
        /// </summary>
        /// <param name="sourcePath">source file</param>
        /// <param name="destPath">destination path</param>
        /// <returns></returns>
        [HttpPost("/copy")]
        public IActionResult Copy([FromForm] string sourcePath, [FromForm] string destPath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || !System.IO.File.Exists(sourcePath))
                return BadRequest("Invalid source file path.");
            if (string.IsNullOrWhiteSpace(destPath))
                return BadRequest("Invalid destination path.");

            //if destination path is a directory, combine with source file name 
            if (Directory.Exists(destPath))
            {
                var fileName = Path.GetFileName(sourcePath);
                destPath = Path.Combine(destPath, fileName);
            }

            try
            {
                System.IO.File.Copy(sourcePath, destPath, overwrite: true);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error copying file: {Source} -> {Dest}", sourcePath, destPath);
                return StatusCode(500, "Error copying file.");
            }
        }
    }
}