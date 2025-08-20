using Microsoft.AspNetCore.Mvc;
using Microsoft.Win32;
using System.IO;
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

        [HttpGet]
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
                    Type = GetFileTypeNameByExtension(info.Extension),
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
                    Folder = directory.Parent.FullName,                    
                    Type = FileFolder,
                    LastModified = directory.LastWriteTime,
                    FullPath = d
                };
            }).ToList();

            return Ok(new
            {
                Results = fileDetails.Concat(directoryDetails),
                FileCount = fileDetails.Count,
                DirectoryCount = directoryDetails.Count,                
            });
        }

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

        private string GetFileTypeNameByExtension(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
            {
                return string.Empty;
            }

            // Ensure the extension starts with a dot
            if (!extension.StartsWith("."))
            {
                extension = "." + extension;
            }

            string fileTypeName = string.Empty;

            try
            {
                // Open the HKEY_CLASSES_ROOT key for the extension
                using (RegistryKey extensionKey = Registry.ClassesRoot.OpenSubKey(extension))
                {
                    if (extensionKey != null)
                    {
                        // Get the default value (ProgID) associated with the extension
                        string progId = extensionKey.GetValue(null) as string;

                        if (!string.IsNullOrEmpty(progId))
                        {
                            // Open the HKEY_CLASSES_ROOT key for the ProgID
                            using (RegistryKey progIdKey = Registry.ClassesRoot.OpenSubKey(progId))
                            {
                                if (progIdKey != null)
                                {
                                    // The default value of the ProgID key often contains the friendly name
                                    fileTypeName = progIdKey.GetValue(null) as string;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Handle potential exceptions during Registry access
                Console.WriteLine($"Error accessing Registry: {ex.Message}");
                fileTypeName = string.Empty;
            }

            return fileTypeName ?? string.Empty; // Return empty string if no name found
        }
    }
}