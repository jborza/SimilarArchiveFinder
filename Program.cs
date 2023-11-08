using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using System.Threading;

namespace SimilarArchiveFinder
{
    class Program
    {
        Dictionary<string, long> fileSizes;

        static void Main(string[] args)
        {
            Program p = new Program();
            p.Run();
        }

        public Program()
        {
            fileSizes = new Dictionary<string, long>();
        }

        void Run()
        {
            //scan folders, find similar ones
            string pattern = "*.7z";
            //var files = Directory.GetFiles(".", pattern).OrderBy(x => x);
            Directory.SetCurrentDirectory(".");
            var files = Directory.GetFiles(".", pattern).OrderBy(x => x);
            string delete = "delete";
            Directory.CreateDirectory(delete);
            int fileCount = files.Count();
            int fileIdx = 0;
            foreach (var file in files)
            {
                fileIdx++;
                //compare with all of the other on the list, going down
                long mySize = GetUncompressedFileSize(file);
                Console.WriteLine(file + " size=" + mySize);
                HashSet<string> similarFiles = new HashSet<string>();
                similarFiles.Add(file);
                foreach (var otherFile in files.Skip(fileIdx))
                {
                    //check file size and the name
                    long otherFileSize = GetUncompressedFileSize(otherFile);
                    double ratio = mySize / (double)otherFileSize;
                    if (ratio < 1)
                        ratio = otherFileSize / (double)mySize;
                    bool fileSizeMatch = ratio < 1.01d;
                    //match name
                    double similarity = Similarity(file, otherFile);
                    bool nameMatch = similarity > 0.7;
                    bool bothMatch = fileSizeMatch && nameMatch;
                    //Console.WriteLine(otherFile + " match = " + bothMatch);
                    if (bothMatch)
                        similarFiles.Add(otherFile);
                    if (fileIdx == 1)
                        Console.Write(".");
                }
                if (similarFiles.Count > 1)
                {
                    Console.WriteLine(similarFiles.Count);
                    //find the one with the shortest filename, keep this and remove the others
                    int minimalLength = similarFiles.Min(p => p.Length);
                    string shortestName = similarFiles.FirstOrDefault(p => p.Length == minimalLength);
                    foreach (string name in similarFiles)
                    {
                        if (name == shortestName)
                            continue;
                        try
                        {
                            File.Move(name, Path.Combine(Directory.GetCurrentDirectory(), delete, Path.GetFileName(name)));
                        }
                        catch { }
                    }
                }
            }
        }

        private double Similarity(string file, string otherFile)
        {
            int stepsToSame = LevenshteinDistance(file, otherFile);
            return (1.0 - ((double)stepsToSame / (double)Math.Max(file.Length, otherFile.Length)));
        }

        public static int LevenshteinDistance(string source, string target)
        {
            // degenerate cases
            if (source == target) return 0;
            if (source.Length == 0) return target.Length;
            if (target.Length == 0) return source.Length;

            // create two work vectors of integer distances
            int[] v0 = new int[target.Length + 1];
            int[] v1 = new int[target.Length + 1];

            // initialize v0 (the previous row of distances)
            // this row is A[0][i]: edit distance for an empty s
            // the distance is just the number of characters to delete from t
            for (int i = 0; i < v0.Length; i++)
                v0[i] = i;

            for (int i = 0; i < source.Length; i++)
            {
                // calculate v1 (current row distances) from the previous row v0

                // first element of v1 is A[i+1][0]
                //   edit distance is delete (i+1) chars from s to match empty t
                v1[0] = i + 1;

                // use formula to fill in the rest of the row
                for (int j = 0; j < target.Length; j++)
                {
                    var cost = (source[i] == target[j]) ? 0 : 1;
                    v1[j + 1] = Math.Min(v1[j] + 1, Math.Min(v0[j + 1] + 1, v0[j] + cost));
                }

                // copy v1 (current row) to v0 (previous row) for next iteration
                for (int j = 0; j < v0.Length; j++)
                    v0[j] = v1[j];
            }

            return v1[target.Length];
        }


        private long GetUncompressedFileSize(string fileName)
        {
            if (fileSizes.ContainsKey(fileName))
                return fileSizes[fileName];

            //sample:
            //"c:\Program Files\7-Zip\7z.exe" l avengrgs.7z | grep - e "files" | awk '{printf $3}'
            var nameWithoutFolder = fileName.Substring(fileName.LastIndexOf("\\") + 1);
            var si = new ProcessStartInfo()
            {
                Arguments = $"l \"{nameWithoutFolder}\"",
                FileName = @"c:\Program Files\7-Zip\7z.exe",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Maximized
            };
            Process p = Process.Start(si);

            string processOutput = null;
            Thread ot = new Thread(() => { processOutput = p.StandardOutput.ReadToEnd(); });
            ot.Start();

            p.WaitForExit();
            ot.Join();
            string response = processOutput;
            //read the last line 
            string[] responseInLines = response.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            string lastLine = responseInLines.Last();
            var sizeString = lastLine.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[2];
            fileSizes[fileName] = long.Parse(sizeString);
            return fileSizes[fileName];
        }

        private long GetFileSize(string fileName)
        {
            if (fileSizes.ContainsKey(fileName))
                return fileSizes[fileName];
            FileInfo fi = new FileInfo(fileName);
            fileSizes[fileName] = fi.Length;
            return fileSizes[fileName];
        }
    }
}
