namespace coreAPI.Classes
{
    public class M3UOptions
    {
        public string FilePath { get; set; } = "import/lista.m3u";
        public string DatabasePath { get; set; } = "import/m3u.sqlite";
        public int BackupRetention { get; set; } = 3; // nº de copias a retener
    }
}
