using Newtonsoft.Json.Linq;
using PhotoOrganizer.Components;
using System;
using System.CodeDom;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace PhotoOrganizer.Services
{
    internal class CopyService
    {
        private const int PACKAGE_SIZE = 50;

        private readonly LogService _logService;
        private readonly Regex _regexPattern = new Regex(":");
        private readonly ParallelOptions _parallelOptions = new ParallelOptions() { MaxDegreeOfParallelism = PACKAGE_SIZE / 2 };
        private readonly Dictionary<string, string[]> _folders = new Dictionary<string, string[]>();

        private readonly Dictionary<string, string> _foldersSearch;
        private readonly Dictionary<string, string> _excludesSearch;

        public CopyService(LogService logService)
        {
            _logService = logService;
            if (!File.Exists("CommonExtensions.json"))
            {
                throw new FileNotFoundException("CommonExtensions.json");
            }

            if (!File.Exists("Excludes.json"))
            {
                throw new FileNotFoundException("Excludes.json");
            }

            try
            {
                var json = JObject.Parse(File.ReadAllText("CommonExtensions.json"));
                foreach (var property in json.Properties())
                {
                    if (!_folders.ContainsKey(property.Path))
                    {
                        _folders.Add(property.Path, json[property.Path].ToObject<string[]>());
                    }
                }

                _foldersSearch = _folders
                    .SelectMany(x => x.Value.Select(o => new
                    {
                        Target = x.Key,
                        Key = o
                    }))
                    .GroupBy(o => o.Key)
                    .ToDictionary(x => x.Key.ToLowerInvariant(), x => x.First().Target);

                _excludesSearch = JArray.Parse(File.ReadAllText("Excludes.json"))
                    .ToObject<string[]>()
                    .GroupBy(x => x)
                    .ToDictionary(x => x.Key, x => x.Key);
            }
            catch (Exception ex)
            {
                throw new Exception($"Invalid json.(CommonExtensions.json), message : {ex.Message}");
            }
        }

        internal void Copy(string source, string destination, bool deleteFile = false)
        {
            _logService.Info($"Searching directory: {source}");
            var filesUnderTheDirectory = DirSearch(source);
            _logService.Info($"{filesUnderTheDirectory.Count} files found.");

            _logService.Info($"Analyzing files");
            var fileInformationDictionary = new ConcurrentDictionary<string, FileInformation>();
            Work(filesUnderTheDirectory.ToArray(), file =>
            {
                fileInformationDictionary.GetOrAdd(file, GetFileInformation(file));
            });
            _logService.Info($"Analyzing completed.");

            _logService.Info($"Copying files to directory..");
            WorkInParallel(fileInformationDictionary.Values.ToArray(), file =>
            {
                DateTime date = default; 
                if (file.PhotoTaken.HasValue)
                {
                    date = file.PhotoTaken.Value;
                }
                else
                {
                    date = file.CreationTime < file.LastWriteTime ? file.CreationTime : file.LastWriteTime;
                }

                var ext = Path.GetExtension(file.Path).ToLowerInvariant();
                var parentFolderName = "others";

                if (_excludesSearch.ContainsKey(ext)) // then exclude file
                {
                    return;
                }

                if (_foldersSearch.ContainsKey(ext))
                {
                    parentFolderName = _foldersSearch[ext];
                }

                var month = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(date.Month);
                var destDirectory = Path.Combine(destination, parentFolderName, date.Year.ToString(), $"{date.Month.ToString("00")}_{month}");
                var destPath = Path.Combine(destDirectory, Path.GetFileName(file.Path));

                if (!Directory.Exists(destDirectory))
                {
                    Directory.CreateDirectory(destDirectory);
                }

                int i = 0;
                while (File.Exists(destPath))
                {
                    i++;
                    var next = $"{Path.GetFileNameWithoutExtension(file.Path)}_{i}{ext}";
                    destPath = Path.Combine(destDirectory, next);
                }

                System.IO.File.Copy(file.Path, destPath);
                if (deleteFile)
                {
                    File.Delete(file.Path);
                }
            });
            _logService.Info($"Copy completed.");
        }

        void Work<TObject>(TObject[] array, Action<TObject> action)
        {
            using (var progress = new ProgressBar())
            {
                for (int i = 0; i < array.Length; i++)
                {
                    var item = array[i];
                    action.Invoke(item);

                    // 100  array.Length
                    // x    i
                    // -----------
                    // x = (100 * i) / array.Length
                    var perc = (100 * i) / array.Length;
                    var report = (double)perc / 100;
                    progress.Report(report);
                }
            }
        }

        void WorkInParallel<TObject>(IEnumerable<TObject> array, Action<TObject> action)
        {
            var total = array.Count();
            var lockObject = new object();
            int status = 0;

            var top = Console.CursorTop;
            using (var progress = new ProgressBar())
            {
                Parallel.ForEach(array, _parallelOptions, item =>
                {
                    action.Invoke(item);
                    lock (lockObject)
                    {
                        status++;
                    }

                    var percent = (100 * status) / total;
                    progress.Report((double)percent / 100);
                });
            }
        }

        FileInformation GetFileInformation(string path)
        {
            var fileInfo = new FileInfo(path);
            return new FileInformation()
            {
                Path = path,
                CreationTime = fileInfo.CreationTime,
                LastWriteTime = fileInfo.LastWriteTime,
                PhotoTaken = path.GetPhotoTaken()
            };
        }

        List<string> DirSearch(string sourceDirectory)
        {
            var response = new List<string>();

            try
            {
                foreach (var file in Directory.GetFiles(sourceDirectory))
                {
                    response.Add(file);
                }

                foreach (var subDirectioy in Directory.GetDirectories(sourceDirectory))
                {
                    response.AddRange(DirSearch(subDirectioy));
                }
            }
            catch (Exception exception)
            {
                _logService.Error(exception.Message);
            }

            return response;
        }

        class FileInformation
        {
            public string Path { get; set; }
            public DateTime CreationTime { get; set; }
            public DateTime LastWriteTime { get; set; }
            public DateTime? PhotoTaken { get; set; }
        }
    }
}