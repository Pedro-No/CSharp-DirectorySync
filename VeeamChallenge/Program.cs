using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;

namespace VeeamChallenge {
    internal class Program {
        static void Main(string[] args) {
            // Check correct number of arguments
            if (args.Length != 4) {
                Console.WriteLine("Usage: VeeamChallenge.exe <sourcePath> <replicaPath> <logDesiredPath> <syncIntervalInSeconds>");
                Console.ReadKey();
                return;
            }

            // Parse arguments
            string sourcePath = args[0];
            string replicaPath = args[1];
            string logDesiredPath = args[2];

            if (!int.TryParse(args[3], out int syncInterval)) {
                Console.WriteLine("Invalid syncInterval value.");
                Console.ReadKey();
                return;
            }

            while (true) {
                SyncFunction(sourcePath, replicaPath, logDesiredPath);
                // Convert seconds to milliseconds
                Thread.Sleep(syncInterval * 1000); 
            }
        }
        static void SyncFunction(string sourcePath, string replicaPath, string logDesiredPath) {
            Console.Clear();
            string logName = "log.txt";
            string logText = $"Sync started @ {DateTime.Now.ToString()} \r\n";

            string logPath = $@"{logDesiredPath}\{logName}";

            //Console log start time
            Console.WriteLine(logText);

            // Compare files in source and replica
            string[] comparedFiles = CompareFilesInDirectories(replicaPath, sourcePath);

            // Compare subdirectories in source and replica
            string[] comparedFolder = CompareFoldersInDirectories(replicaPath, sourcePath);

            // Delete replica files that don't exist in source
            string deleteFileLog = DeleteReplicaFiles(comparedFiles);

            // Delete unwanted replica folders
            string deleteFoldersLog = DeleteReplicaFolders(comparedFolder);

            // Copy files and folders from source to replica
            string copyFileLog = CopySource(sourcePath, replicaPath);

            // Creates the full string for the log file
            logText += deleteFoldersLog + deleteFileLog + copyFileLog;

            // Creates the Log file
            LogFileCreation(logPath, logText);
        }
        static string[] CompareFilesInDirectories(string replicaPath, string sourcePath) {
            string[] sourceDirList = Directory.GetFiles(sourcePath);
            string[] replicaDirList = Directory.GetFiles(replicaPath);

            foreach (string sourceFile in sourceDirList) {
                foreach (string replicaFile in replicaDirList.ToList()) {
                    if (Path.GetFileName(sourceFile) == Path.GetFileName(replicaFile) && AreFilesEqual(replicaFile, sourceFile)) {
                        replicaDirList = replicaDirList.Where(file => file != replicaFile).ToArray();
                    }
                }
            }
            return replicaDirList;
        }
        static string[] CompareFoldersInDirectories(string replicaPath, string sourcePath) {
            string[] sourceDirList = Directory.GetDirectories(sourcePath);
            string[] replicaDirList = Directory.GetDirectories(replicaPath);
            string[] subdirRecursiveList = new string[0];

            if (sourceDirList.Length > 0) {
                foreach (string sourceDir in sourceDirList) {
                    if (replicaDirList.Length > 0) {
                        foreach (string replicaDir in replicaDirList.ToList()) {
                            if (Path.GetFileName(sourceDir) == Path.GetFileName(replicaDir) && CompareFilesInDirectories(replicaDir, sourceDir).Length == 0) {
                                replicaDirList = replicaDirList.Where(dir => dir != replicaDir).ToArray();
                            }
                            if (Path.GetFileName(sourceDir) == Path.GetFileName(replicaDir)) {

                                subdirRecursiveList = CompareFoldersInDirectories(replicaDir, sourceDir);
                            }
                        }
                    }
                }
            }

            string[] combinedArray = replicaDirList.Concat(subdirRecursiveList).ToArray();

            return combinedArray;
        }
        static string DeleteReplicaFiles(string[] replicaDirList) {

            string deleteLog = "";

            if (replicaDirList.Length == 0) {
                deleteLog = deleteLog + "No files to delete \r\n";
                Console.WriteLine(deleteLog);
            }
            else {
                foreach (string name in replicaDirList) {
                    try {
                        File.Delete(name);
                        Console.WriteLine($"deleted: {name} " + DateTime.Now.ToString());
                        deleteLog = deleteLog + $"deleted: {name} -- { DateTime.Now.ToString()} \r\n";
                    }
                    catch (Exception e) {
                        Console.WriteLine($"Failed to delete {name}, exception: ");
                        Console.WriteLine(e.Message);
                        deleteLog = deleteLog + $"{e.Message} -- {DateTime.Now.ToString()} \r\n";
                    }
                }
            }
            return deleteLog;
        }
        static string DeleteReplicaFolders(string[] replicaDirList) {
            string deleteLog = "";

            if (replicaDirList.Length == 0) {
                deleteLog = deleteLog + "No Folders to delete \r\n";
                Console.WriteLine(deleteLog);
            }
            else {
                foreach (string name in replicaDirList) {
                    try {
                        Directory.Delete(name, true);
                        Console.WriteLine($"deleted folder: {name} and all its files" + DateTime.Now.ToString());
                        deleteLog = deleteLog + $"deleted folder: {name} and all its files -- {DateTime.Now.ToString()} \r\n";
                    }
                    catch (Exception e) {
                        Console.WriteLine($"Failed to delete folder {name}, exception: ");
                        Console.WriteLine(e.Message);
                        deleteLog = deleteLog + $"{e.Message} -- {DateTime.Now.ToString()} \r\n";
                    }
                }
            }
            return deleteLog;
        }
        static string CopySource(string sourcePath, string replicaPath) {

            string copyLog = "\r\n";

            string[] sourceFiles = Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories);

            if (sourceFiles.Length == 0) {
                copyLog = copyLog + "no files to copy";
                Console.WriteLine(copyLog);
            } else {
              foreach (string sourceFile in sourceFiles) {
                    try {
                        // Get the relative path of the source file
                        string relativePath = GetRelativePath(sourcePath, sourceFile);

                        // Create the corresponding directory structure in the replica directory
                        string replicaFilePath = Path.Combine(replicaPath, relativePath);
                        string replicaFileDirectory = Path.GetDirectoryName(replicaFilePath);

                        // Ensure the directory exists
                        Directory.CreateDirectory(replicaFileDirectory);

                        // Copy the file to the replica directory
                        File.Copy(sourceFile, replicaFilePath, false); 

                        Console.WriteLine($"Copied file: {sourceFile} to {replicaFilePath} at {DateTime.Now}");
                        copyLog += $"Copied file: {sourceFile} to {replicaFilePath} -- {DateTime.Now.ToString()} \r\n";
                    }
                    catch (Exception e) {
                        Console.WriteLine($"Failed to copy file {sourceFile}. Exception: {e.Message}");
                        copyLog += $"Failed to copy file {sourceFile}. Exception: {e.Message} -- {DateTime.Now.ToString()} \r\n";
                    }
                }
            }
            return copyLog;
        }
        static bool AreFilesEqual(string file1, string file2) {
            using (var md5 = MD5.Create()) {
                using (var stream1 = File.OpenRead(file1))
                using (var stream2 = File.OpenRead(file2)) {
                    byte[] hash1 = md5.ComputeHash(stream1);
                    byte[] hash2 = md5.ComputeHash(stream2);

                    return StructuralComparisons.StructuralEqualityComparer.Equals(hash1, hash2);
                }
            }
        }
        static string GetRelativePath(string fromPath, string toPath) {

            Uri fromUri = new Uri(fromPath.EndsWith(Path.DirectorySeparatorChar.ToString()) ? fromPath : fromPath + Path.DirectorySeparatorChar);
            Uri toUri = new Uri(toPath);

            Uri relativeUri = fromUri.MakeRelativeUri(toUri);

            string relativePath = Uri.UnescapeDataString(relativeUri.ToString());

            return relativePath.Replace('/', Path.DirectorySeparatorChar);
        }
        static void LogFileCreation(string logPath, string logText) {
            if (!File.Exists(logPath)) {
                File.WriteAllText(logPath, logText);
                Console.WriteLine("\r\n" + "new log file created");
            }
            else {
                File.WriteAllText(logPath, logText);
                Console.WriteLine("\r\n" + "previous log file overwritten");
            }
        }
    }
}
