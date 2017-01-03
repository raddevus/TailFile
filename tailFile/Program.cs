using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.AccessControl;

namespace tailFile
{
    class Program
    {
        enum filetype { None, UTF8, Unicode,ASCII,Big };
        private static System.Timers.Timer fileWatcher;
        private static string[] inArgs;
        private static Int64 currentFileSize;
        private static bool firstTime = true;
        private static string stopString;
        private static filetype currentFileType;
        
        static void Main(string[] args)
        {
            // Capture the args for use with timer
            inArgs = args;

            switch (args.Length)
            {
                case  2:
                {
                    DisplayBytes(args);
                    break;
                }
                case 3:
                {
                    UserArgSetsFileType();

                    if (currentFileType != filetype.None)
                    {
                        // this indicates that the 3rd arg was a filetype from
                        // the user so they just want to view a number of bytes
                        // and exit, but they were trying to force a filetype
                        DisplayBytes(args);
                    }
                    else
                    {
                        if (args[2].ToLower() == "-c")
                        {
                            DisplayBytes(args);
                            fileWatcher = new System.Timers.Timer();
                            fileWatcher.Elapsed += new System.Timers.ElapsedEventHandler(fileWatcher_Elapsed);
                            fileWatcher.Interval = 500;
                            fileWatcher.Start();
                            WaitForMessages();
                        }
                        else
                        {
                            Console.WriteLine("Argument 3 is incorrect. Please check the [filetype] or try -c");
                            ExitProc();
                        }
                    }
                    break;
                }
                case 4:
                {
                    UserArgSetsFileType();
                    if (args[3].ToLower() == "-c")
                    {
                        DisplayBytes(args);
                        fileWatcher = new System.Timers.Timer();
                        fileWatcher.Elapsed += new System.Timers.ElapsedEventHandler(fileWatcher_Elapsed);
                        fileWatcher.Interval = 500;
                        fileWatcher.Start();
                        WaitForMessages();
                    }
                    else
                    {
                        Console.WriteLine("Argument 3 is incorrect. Please check the [filetype] try -c");
                        ExitProc();
                    }
                    break;
                }
                default:
                {
                    Console.WriteLine("Incorrect number of arguments supplied.");
                    ExitProc();
                    break;
                }
            }
        }

        static void UserArgSetsFileType()
        {
            // if the last arg is uni or utf, 
            // allow user to force the program into a specific mode
            if (inArgs[inArgs.Length - 1].ToLower() == "uni")
            {
                currentFileType = filetype.Unicode;
            }
            else if (inArgs[inArgs.Length - 1].ToLower() == "utf")
            {
                currentFileType = filetype.UTF8;
            }
            else if (inArgs[inArgs.Length - 1].ToLower() == "big")
            {
                currentFileType = filetype.Big;
            }
        }

        static filetype DetermineFileType()
        {
            // now supports UNICODE, UTF8, Big Endian and ASCII
            FileStream reader = null;
            try
            {
                reader = new FileStream(inArgs[0], FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                byte [] readBuffer = new byte[2];
                reader.Read(readBuffer, 0, 2);
            
                //Check for BOM for unicode file
                if (readBuffer[0] == 255)
                {
                    if (readBuffer[1] == 254)
                    {
                        return filetype.Unicode;
                    }
                }
                // check for BOM for UTF8
                if (readBuffer[0] == 239)
                {
                    if (readBuffer[1] == 187)
                    {
                        return filetype.UTF8;
                    }
                }
                // check for BOM for BigEndian
                if (readBuffer[0] == 254)
                {
                    if (readBuffer[1] == 255)
                    {
                        return filetype.Big;
                    }
                }
                return filetype.ASCII;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                ExitProc();
                return filetype.ASCII;
            }
            finally
            {
                reader.Close();
            }
        }

        static void WaitForMessages()
        {
            do
            {
                stopString = Console.ReadLine();
            }
            while (stopString.ToLower() != "q");
        }

        public static void ExitProc()
        {
            Console.WriteLine("Usage: tailFile <filename> <numberOfBytesToDisplay> [-c]ontinue tailing  [filetype]");
            Console.WriteLine(@"Ex.1: tailFile c:\test\mydata.dat 500");
            Console.WriteLine(@"Ex.1: tailFile c:\test\mydata.dat 500 utf8");
            Console.WriteLine(@"Ex.2: tailFile c:\test\mydata.dat 500 big");
            Console.WriteLine(@"Ex.2: tailFile c:\test\mydata.dat 500 -c");
            Console.WriteLine(@"Ex.2: tailFile c:\test\mydata.dat 500 utf8 -c");
            Console.WriteLine(@"Ex.2: tailFile c:\test\mydata.dat 500 uni -c");
            System.Diagnostics.Process proc = System.Diagnostics.Process.GetCurrentProcess();
            Console.Out.Flush();
            proc.Kill();
        }

        static Int64 GetFileSize()
        {
            FileStream fs = null;
            Int64 length;
            try
            {
                fs = new FileStream(inArgs[0], FileMode.Open,
                        FileAccess.Read, FileShare.ReadWrite);
                length = fs.Length;
                return length;
            }
            finally
            {
                fs.Close();
            }
        }

        static void fileWatcher_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            DisplayUpdatedBytes();
        }

        public static void DisplayUpdatedBytes()
        {
            Int64 checkedSize = GetFileSize();
            if (checkedSize > currentFileSize)
            {
                string[] vals = { inArgs[0], (checkedSize - currentFileSize).ToString() };
                DisplayBytes(vals);
                currentFileSize = checkedSize;
                // Next line is test code
                //Console.WriteLine(string.Format("checkedSize : {0} currentFileSize {1}",checkedSize, currentFileSize));
            }
        }

        public static void DisplayBytes(string [] args)
        {
            if (currentFileType == filetype.None)
            {
                currentFileType = DetermineFileType();
            }

            FileStream fs = null;
            try
            {
                long numberOfBytesToDisplay = Convert.ToInt32(args[1]);

                // This opens the file for read, and does a FileShare that 
                // says the file can be opened for read / write by other processes.
                // This solves the problem of reading from a file that is already opened
                // by another process.  Very cool.
                fs = new FileStream(args[0], FileMode.Open,
                    FileAccess.Read, FileShare.ReadWrite);

                //ternary check insures that the attempted read of bytes is never more
                // than the total bytes in file.
                numberOfBytesToDisplay = numberOfBytesToDisplay < fs.Length ? numberOfBytesToDisplay : fs.Length;
                
                byte[] buf = new byte[numberOfBytesToDisplay];

                if (firstTime) // only display msg one time
                {
                    Console.WriteLine(string.Format("{0} = {1:#,#0} bytes.{2}", args[0], fs.Length, Environment.NewLine));
                    firstTime = false;
                }
                fs.Seek(-numberOfBytesToDisplay, SeekOrigin.End);
                fs.Read(buf, 0, (int)numberOfBytesToDisplay);

                // handles the conversion of the bytes (for output) according to the filetype
                switch (currentFileType)
                {
                    case filetype.Unicode:
                        {
                            buf = System.Text.Encoding.Convert(Encoding.Unicode, Encoding.ASCII, buf);
                            break;
                        }
                    case filetype.UTF8:
                        {
                            buf = System.Text.Encoding.Convert(Encoding.UTF8, Encoding.ASCII, buf);
                            break;
                        }
                    case filetype.Big:
                        {
                            buf = System.Text.Encoding.Convert(Encoding.BigEndianUnicode, Encoding.ASCII, buf);
                            break;
                        }
                }
                StringBuilder sbOutstring = new StringBuilder();
                foreach (byte b in buf)
                {
                    sbOutstring.Append(Convert.ToChar(b));
                }
                // changed line to .Write from WriteLine so I do not add the newline
                Console.Write(sbOutstring);
                currentFileSize = fs.Length;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                ExitProc();
            }
            finally
            {
                if (fs != null)
                {
                    fs.Close();
                }
            }
        }
    }
}
