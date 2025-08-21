using Microsoft.AspNetCore.Mvc;
using TestProject.Models;

namespace TestProject.Controllers {
    [ApiController]
    [Route("[controller]")]
    public class TestController : ControllerBase {
        private const string FileFolder = "File folder";
        private static string DefaultFolder = "";
        private readonly ILogger<TestController> _logger;
        private readonly IConfiguration _configuration;

        public TestController(ILogger<TestController> logger, IConfiguration configuration) {
            _logger = logger;
            _configuration = configuration;

            DefaultFolder = _configuration["ApiSettings:BaseDirectory"];
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
                path = DefaultFolder;
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
                path = DefaultFolder;
            }

            if (!IsPathWithinFolder(path, DefaultFolder))
                return BadRequest("Destination path is not within sandbox.");

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
                path = DefaultFolder;
            }

            if (!IsPathWithinFolder(path, DefaultFolder))
                return BadRequest("Destination path is not within sandbox.");

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
            if (string.IsNullOrWhiteSpace(path))
                return BadRequest("Invalid file or directory path.");
            if (!IsPathWithinFolder(path, DefaultFolder))
                return BadRequest("Destination path is not within sandbox.");

            try
            {
                if (System.IO.File.Exists(path))
                {
                    System.IO.File.Delete(path);
                }
                else if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
                else
                {
                    return NotFound("File or directory does not exist.");
                }
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting file or directory: {Path}", path);
                return StatusCode(500, "Error deleting file or directory.");
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
            if (string.IsNullOrWhiteSpace(sourcePath))
                return BadRequest("Invalid source file or directory path.");
            if (string.IsNullOrWhiteSpace(destPath))
                return BadRequest("Invalid destination path.");
            if (!IsPathWithinFolder(destPath, DefaultFolder))
                return BadRequest("Destination path is not within sandbox.");

            try
            {
                if (System.IO.File.Exists(sourcePath))
                {
                    // If destination path is a directory, combine with source file name
                    if (Directory.Exists(destPath))
                    {
                        var fileName = Path.GetFileName(sourcePath);
                        destPath = Path.Combine(destPath, fileName);
                    }
                    System.IO.File.Move(sourcePath, destPath, overwrite: true);
                }
                else if (Directory.Exists(sourcePath))
                {
                    // If destPath exists and is a file, error
                    if (System.IO.File.Exists(destPath))
                        return BadRequest("Cannot move directory to a file path.");

                    // If destPath is an existing directory, move into it
                    if (Directory.Exists(destPath))
                    {
                        var dirName = Path.GetFileName(sourcePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                        destPath = Path.Combine(destPath, dirName);
                    }
                    Directory.Move(sourcePath, destPath);
                }
                else
                {
                    return NotFound("Source file or directory does not exist.");
                }
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error moving file or directory: {Source} -> {Dest}", sourcePath, destPath);
                return StatusCode(500, "Error moving file or directory.");
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
            if (string.IsNullOrWhiteSpace(sourcePath))
                return BadRequest("Invalid source file path.");
            if (string.IsNullOrWhiteSpace(destPath))
                return BadRequest("Invalid destination path.");
            if (!IsPathWithinFolder(destPath, DefaultFolder))
                return BadRequest("Destination path is not within sandbox.");
            try
            {
                if (System.IO.File.Exists(sourcePath))
                {
                    // If destPath is a directory, combine with source file name
                    if (Directory.Exists(destPath))
                    {
                        var fileName = Path.GetFileName(sourcePath);
                        destPath = Path.Combine(destPath, fileName);
                    }
                    System.IO.File.Copy(sourcePath, destPath, overwrite: true);
                }
                else if (Directory.Exists(sourcePath))
                {
                    // If destPath exists and is a file, error
                    if (System.IO.File.Exists(destPath))
                        return BadRequest("Cannot copy directory to a file path.");

                    // If destPath is an existing directory, copy into it
                    if (Directory.Exists(destPath))
                    {
                        var dirName = Path.GetFileName(sourcePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                        destPath = Path.Combine(destPath, dirName);
                    }
                    CopyDirectory(sourcePath, destPath);
                }
                else
                {
                    return BadRequest("Source path does not exist.");
                }
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error copying: {Source} -> {Dest}", sourcePath, destPath);
                return StatusCode(500, "Error copying file or directory.");
            }
        }

        /// <summary>
        /// Recursively copy a directory
        /// </summary>
        /// <param name="sourceDir"></param>
        /// <param name="destDir"></param>
        private void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var destFile = Path.Combine(destDir, Path.GetFileName(file));
                System.IO.File.Copy(file, destFile, overwrite: true);
            }
            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                var destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
                CopyDirectory(dir, destSubDir);
            }
        }

        /// <summary>
        /// Check if a given path is within a specified folder.
        /// </summary>
        /// <param name="itemPath"></param>
        /// <param name="folderPath"></param>
        /// <returns></returns>
        private static bool IsPathWithinFolder(string itemPath, string folderPath)
        {
            if (string.IsNullOrWhiteSpace(itemPath) || string.IsNullOrWhiteSpace(folderPath))
                return false;
           
            var itemFullPath = Path.GetFullPath(itemPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var folderFullPath = Path.GetFullPath(folderPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            
            return itemFullPath.StartsWith(folderFullPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || string.Equals(itemFullPath, folderFullPath, StringComparison.OrdinalIgnoreCase);
        }
    }
}