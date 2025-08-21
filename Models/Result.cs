namespace TestProject.Models
{
    public class Result
    {
        public string? Name { get; set; }
        public string? Folder { get; set; }
        public long? Size { get; set; }  
        public DateTime LastModified { get; set; }
        public string? Type { get; set; }
        public string? FullPath { get; set; }
    }
}
