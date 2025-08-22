using System.Security.Cryptography;
using System.Text;

namespace TransferTool
{
    internal class Program
    {
        static string SOURCE_FILE_PATH = @"C:\300.Rise.of.an.Empire.2014.mp4"; // size: 1.64 GB
        static string TARGET_FILE_PATH = @"D:\Temp\300.Rise.of.an.Empire.2014.mp4";
        static int BUFFER_SIZE = 1024 * 1024; // 1 MB        
        static List<int> RETRY_CHUNK_LIST = new List<int>(); // chunk numbers listed here will be simulated to fail.
        static bool RETRY_ACTIVATED = false; // flag that will help me know the retry is being attempted and for it I should not
                                             // simulate failure of those chunks again.
        static int TOTAL_FILE_CHUNKS = 0; // total number of file chunks to be transferred
        static Queue<int> CHUNK_NUMBER_QUEUE = new Queue<int>();
        static object LOCK_OBJECT = new object();

        static void Main(string[] args)
        {
            Title();
            FileInfo();
            UserPrompt();
            TransferFile();
            RetryFailedChunksIfAny();            

            Console.WriteLine("");
            Console.WriteLine("=== Transfer Completed ===");

            Console.WriteLine("");
            Console.WriteLine("Please press any key to close this window...");

            Console.ReadKey();

        }

        static void Title()
        {
            Console.WriteLine("===========================================");
            Console.WriteLine("=   File Transfer Tool | Hornetsecurity   =");
            Console.WriteLine("=                     Task                =");
            Console.WriteLine("=        Author: Dragan Velichkovski      =");
            Console.WriteLine("===========================================");
            Console.WriteLine("");
        }

        static void FileInfo()
        {
            try
            {
                using (FileStream reader = new FileStream(SOURCE_FILE_PATH, FileMode.Open, FileAccess.Read))
                {
                    Console.WriteLine("--- FILE AND TRANSFER INFO ---");

                    TOTAL_FILE_CHUNKS = (int)Math.Ceiling((decimal)reader.Length / (1024 * 1024));

                    Console.WriteLine($"The source file's size is {reader.Length} bytes or {Math.Round((double)reader.Length / (1024 * 1024 * 1024), 2)} GB.\nThe transfer operation will use {Program.TOTAL_FILE_CHUNKS} 1MB chunks.\nTwo different threads will be running simultaneously to do the transfer.");

                    CHUNK_NUMBER_QUEUE = new Queue<int>(Enumerable.Range(1, TOTAL_FILE_CHUNKS));
                }
            }
            catch (FileNotFoundException ex)
            {
                Console.BackgroundColor = ConsoleColor.Red;
                Console.WriteLine("File not found exception! " + ex.Message);
            }
            catch (Exception ex)
            {
                Console.BackgroundColor = ConsoleColor.Red;
                Console.WriteLine("Exception! " + ex.Message);
            }
            finally
            {
                Console.BackgroundColor = ConsoleColor.Black;
            }
        }

        static void UserPrompt()
        {
            bool error = false;

            Console.WriteLine("");
            Console.WriteLine($"There are {TOTAL_FILE_CHUNKS} which will be transferred to the destination. Please specify which chunk numbers 1-{TOTAL_FILE_CHUNKS} are supposed to be simulated as failed in a comma separated list. For none, please just press enter without an input.");
            string? userInput = Console.ReadLine();

            if (!string.IsNullOrWhiteSpace(userInput))
            {
                string[] chunksFailed = userInput.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(c => c.Trim())
                    .ToArray();

                try
                {
                    foreach (string chunk in chunksFailed)
                    {
                        RETRY_CHUNK_LIST.Add(int.Parse(chunk));
                    }
                }
                catch (FormatException ex)
                {
                    Console.BackgroundColor = ConsoleColor.Red;
                    Console.WriteLine("Format exception! " + ex.Message + ". Please retry!");
                    error = true;
                }
                catch (Exception ex)
                {
                    Console.BackgroundColor = ConsoleColor.Red;
                    Console.WriteLine("Exception! " + ex.Message + ". Please retry!");
                    error = true;
                }
                finally
                {
                    Console.BackgroundColor = ConsoleColor.Black;
                }
            }
            else
            {
                Console.WriteLine("No chunks specified for failure simulation! Proceeding without simulating any failed chunk transfers...");
            }

            if (error)
            {
                RETRY_CHUNK_LIST.Clear();
                UserPrompt();
            }
        }

        static void RetryFailedChunksIfAny()
        {
            // if there were failed chunks, retry their transfer
            if (RETRY_CHUNK_LIST.Count > 0)
            {
                RetryFailedChunks();
            }
        }

        static void TransferFile()
        {
            Console.WriteLine("");

            Thread t1 = new Thread(Intermediary);
            t1.Name = "1";
            Thread t2 = new Thread(Intermediary);
            t2.Name = "2";

            t1.Start();
            t2.Start();

            t1.Join();
            t2.Join();

            CompareSHA1Hashes(SOURCE_FILE_PATH, TARGET_FILE_PATH);
            CompareSHA256Hashes(SOURCE_FILE_PATH, TARGET_FILE_PATH);
        }

        static void Intermediary()
        {
            while(true)
            {
                int chunkNumber;
                lock(LOCK_OBJECT)
                {
                    if (CHUNK_NUMBER_QUEUE.Count == 0) return;

                    chunkNumber = CHUNK_NUMBER_QUEUE.Dequeue();
                }

                TransferFileChunk(SOURCE_FILE_PATH, TARGET_FILE_PATH, BUFFER_SIZE, (chunkNumber - 1) * 1024 * 1024, chunkNumber);
            }
        }

        static void TransferFileChunk(string source, string target, int chunkSize, int currentPosition, int chunkNumber)
        {
            StringBuilder sBuilder;
            int readCount = 0;

            using (FileStream reader = new FileStream(source, FileMode.Open, FileAccess.Read))
            {
                using (FileStream writer = new FileStream(target, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite))
                {
                    // set current position within the stream
                    reader.Seek(currentPosition, SeekOrigin.Begin);

                    // read an 1MB chunk of data considering the current position and store the data in a buffer.
                    // readCount = 0 would mean there is no more data to read and the method should exit the process.
                    byte[] buffer = new byte[chunkSize];
                    readCount = reader.Read(buffer, 0, buffer.Length);
                    if (readCount == 0) return;

                    // make MD5 hash of the buffer data
                    using (MD5 md5 = MD5.Create())
                    {
                        sBuilder = new StringBuilder();
                        byte[] sourceHash = md5.ComputeHash(buffer);
                        for (int i = 0; i < sourceHash.Length; i++)
                        {
                            sBuilder.Append(sourceHash[i].ToString("x2"));
                        }
                    }

                    // try to write the buffer data to the target file
                    try
                    {
                        // simulate the failure of some chunk's transfer
                        if (!RETRY_ACTIVATED && RETRY_CHUNK_LIST.Contains(chunkNumber))
                        {
                            throw new IOException($"Simulated failure of chunk #{chunkNumber}'s transfer!");
                        }

                        // specifying the starting position within the target file where the data will be written
                        writer.Seek(currentPosition, SeekOrigin.Begin);
                        writer.Write(buffer, 0, readCount);
                        writer.Flush();
                    }
                    catch (IOException ex)
                    {
                        Console.BackgroundColor = ConsoleColor.Red;
                        Console.WriteLine($"I/O Exception! {ex.Message}");
                    }
                    catch (Exception ex)
                    {
                        Console.BackgroundColor = ConsoleColor.Red;
                        Console.WriteLine($"Exception! {ex.Message}");
                    }
                    finally
                    {
                        Console.BackgroundColor = ConsoleColor.Black;
                    }

                }
            }

            bool isValid = ValidateTransferFileChunk(sBuilder.ToString(), currentPosition);

            if (!isValid)
            {
                Console.BackgroundColor = ConsoleColor.Red;
            }
            else
            {
                Console.BackgroundColor = ConsoleColor.Black;
            }

            Console.WriteLine($"Chunk: {chunkNumber}, Thread: {Thread.CurrentThread.Name}, Bytes: {readCount}, Position: {currentPosition}, MD5: {sBuilder.ToString()}, Transfer status: {(isValid ? "OK" : "FAIL")}");
        }

        static bool ValidateTransferFileChunk(string sourceHash, int currentPosition)
        {
            StringBuilder sBuilder;

            using (FileStream reader = new FileStream(TARGET_FILE_PATH, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                // set current position within the stream
                reader.Seek(currentPosition, SeekOrigin.Begin);
                // read an 1MB chunk of data considering the current position and store the data in a buffer.
                byte[] buffer = new byte[BUFFER_SIZE];

                int readCount = reader.Read(buffer, 0, buffer.Length);

                // make MD5 hash of the buffer data
                using (MD5 md5 = MD5.Create())
                {
                    byte[] targetHash = md5.ComputeHash(buffer);
                    sBuilder = new StringBuilder();
                    for (int i = 0; i < targetHash.Length; i++)
                    {
                        sBuilder.Append(targetHash[i].ToString("x2"));
                    }
                }

                if (sourceHash == sBuilder.ToString())
                {
                    // source and target hashes match
                    return true;
                }
            }

            // source and target hashes do not match
            return false;
        }

        static string GetSHA1Hash(string filePath)
        {
            StringBuilder sBuilder = new StringBuilder();

            try
            {
                using (FileStream reader = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    using (SHA1 sha1 = SHA1.Create())
                    {
                        byte[] hash = sha1.ComputeHash(reader);
                        for (int i = 0; i < hash.Length; i++)
                        {
                            sBuilder.Append(hash[i].ToString("x2"));
                        }
                    }
                }
            }
            catch (FileNotFoundException ex)
            {
                Console.BackgroundColor = ConsoleColor.Red;
                Console.WriteLine("File not found exception! " + ex.Message);
            }
            catch (Exception ex)
            {
                Console.BackgroundColor = ConsoleColor.Red;
                Console.WriteLine("Exception! " + ex.Message);
            }
            finally
            {
                Console.BackgroundColor = ConsoleColor.Black;
            }

            return sBuilder.ToString();
        }

        static void CompareSHA1Hashes(string sourceFilePath, string targetFilePath)
        {
            Console.WriteLine("");
            Console.WriteLine("--- COMPARE FILE HASHES WITH SHA1 ---");
            Console.WriteLine("");
            string sourceHash = GetSHA1Hash(sourceFilePath);
            string targetHash = GetSHA1Hash(targetFilePath);
            if (sourceHash == targetHash)
            {
                Console.BackgroundColor = ConsoleColor.Green;
                Console.ForegroundColor = ConsoleColor.Black;
                Console.WriteLine($"The source and target files match! SHA1: {sourceHash}.");
            }
            else
            {
                Console.BackgroundColor = ConsoleColor.Red;
                Console.WriteLine($"The source and target files do NOT match!\nSource SHA1: {sourceHash}.\nTarget SHA1: {targetHash}.\nThe failed chunk numbers are {string.Join(", ", RETRY_CHUNK_LIST.Select(c => c.ToString()))}.");
            }
            Console.BackgroundColor = ConsoleColor.Black;
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("");
        }

        static string GetSHA256Hash(string filePath)
        {
            StringBuilder sBuilder = new StringBuilder();
            try
            {
                using (FileStream reader = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    using (SHA256 sha256 = SHA256.Create())
                    {
                        byte[] hash = sha256.ComputeHash(reader);
                        for (int i = 0; i < hash.Length; i++)
                        {
                            sBuilder.Append(hash[i].ToString("x2"));
                        }
                    }
                }
            }
            catch (FileNotFoundException ex)
            {
                Console.BackgroundColor = ConsoleColor.Red;
                Console.WriteLine("File not found exception! " + ex.Message);
            }
            catch (Exception ex)
            {
                Console.BackgroundColor = ConsoleColor.Red;
                Console.WriteLine("Exception! " + ex.Message);
            }
            finally
            {
                Console.BackgroundColor = ConsoleColor.Black;
            }
            return sBuilder.ToString();
        }

        static void CompareSHA256Hashes(string sourceFilePath, string targetFilePath)
        {
            Console.WriteLine("");
            Console.WriteLine("--- COMPARE FILE HASHES WITH SHA256 ---");
            Console.WriteLine("");
            string sourceHash = GetSHA256Hash(sourceFilePath);
            string targetHash = GetSHA256Hash(targetFilePath);
            if (sourceHash == targetHash)
            {
                Console.BackgroundColor = ConsoleColor.Green;
                Console.ForegroundColor = ConsoleColor.Black;
                Console.WriteLine($"The source and target files match! SHA256: {sourceHash}.");
            }
            else
            {
                Console.BackgroundColor = ConsoleColor.Red;
                Console.WriteLine($"The source and target files do NOT match!\nSource SHA256: {sourceHash}.\nTarget SHA256: {targetHash}.\nThe failed chunk numbers are {string.Join(", ", RETRY_CHUNK_LIST.Select(c => c.ToString()))}.");
            }
            Console.BackgroundColor = ConsoleColor.Black;
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("");
        }

        static void RetryFailedChunks()
        {
            Console.WriteLine("");
            Console.WriteLine("--- RETRYING TRANSFER OF THE FAILED CHUNKS ---");
            Console.WriteLine("");

            RETRY_ACTIVATED = true;
            foreach (int chunkNumber in RETRY_CHUNK_LIST)
            {
                TransferFileChunk(SOURCE_FILE_PATH, TARGET_FILE_PATH, BUFFER_SIZE, (chunkNumber - 1) * 1024 * 1024, chunkNumber);
            }

            CompareSHA1Hashes(SOURCE_FILE_PATH, TARGET_FILE_PATH);
            CompareSHA256Hashes(SOURCE_FILE_PATH, TARGET_FILE_PATH);
        }
    }
}
