using System.Security.Cryptography;
using System.Text;

namespace TransferTool
{
    internal class Program
    {
        static string SOURCE_FILE_PATH = @"C:\300.Rise.of.an.Empire.2014.mp4"; // size: 1.64 GB
        static string TARGET_FILE_PATH = @"D:\Temp\300.Rise.of.an.Empire.2014.mp4";
        static int BUFFER_SIZE = 1024 * 1024; // 1 MB

        static void Main(string[] args)
        {
            Title();
            FileInfo();

        }

        public static void Title()
        {
            Console.WriteLine("===========================================");
            Console.WriteLine("=   File Transfer Tool | Hornetsecurity   =");
            Console.WriteLine("=                     Task                =");
            Console.WriteLine("=        Author: Dragan Velichkovski      =");
            Console.WriteLine("===========================================");
            Console.WriteLine("");
        }

        public static void FileInfo()
        {
            int fileChunks = 0;

            try
            {
                using (FileStream reader = new FileStream(SOURCE_FILE_PATH, FileMode.Open, FileAccess.Read))
                {
                    Console.WriteLine("--- FILE AND TRANSFER INFO ---");

                    fileChunks = (int)Math.Ceiling((decimal)reader.Length / (1024 * 1024));

                    Console.WriteLine($"The source file's size is {reader.Length} bytes or {Math.Round((double)reader.Length / (1024 * 1024 * 1024), 2)} GB.\nThe transfer operation will use {fileChunks} 1MB chunks.");


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

        public static void TransferFileChunk(string source, string target, int chunkSize, int currentPosition, int chunkNumber)
        {
            StringBuilder sBuilder;
            int readCount = 0;

            using (FileStream reader = new FileStream(source, FileMode.Open, FileAccess.Read))
            {
                using (FileStream writer = new FileStream(target, FileMode.OpenOrCreate, FileAccess.Write))
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
                        if (chunkNumber == 3 || chunkNumber == 30)
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
                    catch(Exception ex)
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
        }

        public static bool ValidateTransferFileChunk(string sourceHash, int currentPosition)
        {
            StringBuilder sBuilder;

            using (FileStream reader = new FileStream(TARGET_FILE_PATH, FileMode.Open, FileAccess.Read))
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
    }
}
