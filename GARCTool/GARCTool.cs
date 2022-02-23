﻿using System;
using System.IO;
using System.Windows.Forms;
using System.Threading;

namespace GARCTool // If you are including this source file, replace the namespace with the name of your program.
{
    /* Readme
     * GARCTool should usually be operated via the 3 main pack/unpack functions within the GARCTool class.
     * 
     * Contains:
     * GARCTool : Depends on subsequent classes. Methods are as follows...
     *  garcOmni - operates pack/unpack on an arbitrary string with preset paths.
     *  garcPack - Packs up an inFolder path to an outFile garc path, using a temporary working folder.
     *  garcUnpack - Unpacks an inFile garc path to an outFolder path, with an option to skip decompression (rarely needed).
     * 
     * Util : Utility functions for mundane operations.
     * GARC Class and Structures : To unpack GARCs via objects. Packing is done without.
     * 
     * dsdecmp : Tweaked and trimmed from the dsdecmp source code. https://code.google.com/p/dsdecmp
     *  Requires unsafe code to be allowed, as ptrs are needed.
     *  LZ11 : (De)Compress methods...
     *      Decompress(string infile, string outfile)
     *      Compress(string infile, string outfile)
     *      Also contained are the subfunctions which operate via MemoryStream, which can be used instead.
     *  Subclasses:
     *      Exceptions : to fall back on if (de)compression fails with bad files.
     *      Supplementary : LZUtil & IOUtils 
     *      
     * Code Licensing: 
     *      dsdecmp is licensed under the MIT License. 
     *      The rest of GARCTool is licensed under the MIT License as well.
     *      http://opensource.org/licenses/MIT
     */
    #region GARCTool Classes
    public class GARCTool
    {
        public static bool garcOmni(string path, ProgressBar pBar1 = null)
        {
            try // Try packing/unpacking
            {
                if (path.Length == 0 || path == null)
                { throw new ArgumentException("Invalid string path"); }
                if (Directory.Exists(path))
                    garcPack(path, Path.Combine(Directory.GetParent(path).ToString(), Path.GetFileName(path) + ".garc"), Path.Combine(Application.StartupPath, "datapackup"), pBar1);
                else if (File.Exists(path))
                    garcUnpack(path, path + "_", false, pBar1);
                else
                { throw new Exception(path + " is not a folder or file"); }

                return true;
            }
            catch (Exception e)
            {
                if (pBar1.IsDisposed)
                    return false;
                MessageBox.Show("Error!\n\n" + e.ToString());
                System.Media.SystemSounds.Exclamation.Play();
                return false;
            }
        }
        public static bool garcPack(string folderPath, string garcPath, string buildPath, ProgressBar pBar1 = null)
        {
            // Check to see if our input folder exists.
            if (!new DirectoryInfo(folderPath).Exists) { MessageBox.Show("Folder does not exist."); return false; }

            // Check to see if we can make our buildpath.
            if (!new DirectoryInfo(buildPath).Exists)
            {
                try { Directory.CreateDirectory(buildPath); }
                catch { MessageBox.Show(buildPath + " is not a valid directory to work in."); return false; };
            }

            // Okay some basic proofing is done. Proceed.
            
            // Get the paths of the files to pack up.
            string[] filepaths = Directory.GetFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly);

            // Initialize ProgressBar
            if (pBar1 == null) pBar1 = new ProgressBar();
            if (pBar1.InvokeRequired)
                pBar1.Invoke((MethodInvoker)delegate { 
                pBar1.Minimum = 0; pBar1.Step = 1; pBar1.Value = 0; pBar1.Maximum = filepaths.Length; 
                });
            else
                pBar1.Minimum = 0; pBar1.Step = 1; pBar1.Value = 0; pBar1.Maximum = filepaths.Length;

            // Copy the files to the working directory so our compression doesn't overwrite anything.
            for (int i = 0; i < filepaths.Length; i++)
                File.Copy(filepaths[i],Path.Combine(buildPath,Path.GetFileName(filepaths[i])),true);

            // Scan through to see if we have to compress anything.
            filepaths = Directory.GetFiles(buildPath, "*.*", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < filepaths.Length; i++)
            {
                int compressed = Path.GetFileName(filepaths[i]).IndexOf("_");
                if (compressed >= 0)
                {
                    // File needs to be compressed and replaced.
                    string compressedName = Path.Combine(buildPath,Path.GetFileName(filepaths[i]).Substring(compressed+1));
                    try
                    {
                        dsdecmp.Compress(filepaths[i], compressedName);
                        try { File.Delete(filepaths[i]); } // Try to delete the decompressed file as we don't write it to the GARC.
                        catch { MessageBox.Show("Could not delete decompressed file"); }
                    }
                    catch
                    {
                        try { File.Delete(compressedName); } // Something screwed up. This shouldn't happen unless you're fuzzing...
                        catch { MessageBox.Show("Could not delete failed compressed file"); }
                    }
                }
            }
            // Refresh file list
            filepaths = Directory.GetFiles(buildPath, "*.*", SearchOption.TopDirectoryOnly);

            using (MemoryStream newGARC = new MemoryStream())
            using (MemoryStream GARCdata = new MemoryStream())
            using (BinaryWriter gw = new BinaryWriter(newGARC))
            {

                // Write GARC header
                gw.Write((uint)0x47415243); // Write "CRAG"
                gw.Write((uint)0x1C);       // Header Length
                gw.Write((ushort)0xFEFF);   // FEFF BOM
                gw.Write((ushort)0x0400);   //   
                gw.Write((uint)0x4);        //
                gw.Write((uint)0);          // Data Offset
                gw.Write((uint)0);          // File Length
                gw.Write((uint)0);          // FATB chunk last word

                // Write OTAF
                gw.Write((uint)0x4641544F);                     // OTAF                     
                gw.Write((uint)(0xC + 4 * filepaths.Length));   // Section Size 
                gw.Write((ushort)filepaths.Length);             // Count: n 
                gw.Write((ushort)0xFFFF);                       // padding? dunno                  

                // write BTAF jump offsets
                for (int i = 0; i < filepaths.Length; i++)
                    gw.Write((uint)i * 0x10);

                // Start BTAF
                gw.Write((uint)0x46415442); // BTAF
                gw.Write((uint)(0xC + 0x10 * filepaths.Length)); // Chunk Size
                gw.Write((uint)filepaths.Length);
                gw.Write((uint)1);          // garc.btaf.bits = br.ReadUInt32(); 

                uint offset = 0;
                uint largest = 0;
                for (int i = 0; i < filepaths.Length; i++)
                {
                    FileInfo fi = new FileInfo(filepaths[i]);
                    using (FileStream fileStream = new FileStream(filepaths[i], FileMode.Open))
                    {
                        gw.Write((uint)offset);     // Start/Begin Offset
                        uint round = (uint)Math.Ceiling(((double)fi.Length / 4)) * 4;
                        offset += (uint)(round);
                        gw.Write((uint)offset);     // End/Stop Offset
                        gw.Write((uint)fi.Length);  // Length/Size

                        if (fi.Length > largest) largest = (uint)fi.Length;

                        // Write the data to the BMIF data section
                        fileStream.CopyTo(GARCdata);    // then pad with FF's if not /4
                        while (GARCdata.Length % 4 > 0) GARCdata.WriteByte(0xFF);
                    }
                    if (pBar1.InvokeRequired)
                        pBar1.Invoke((MethodInvoker)delegate { pBar1.PerformStep(); });
                    else
                        pBar1.PerformStep();
                }
                // Roundup Offset

                gw.Write((uint)0x46494D42);
                gw.Write((uint)0xC);
                gw.Write((uint)offset);

                uint dataOffset = (uint)newGARC.Length;
                uint garcLength = (uint)(newGARC.Length + GARCdata.Length);
                uint largestFile = (uint)largest;

                gw.Seek(0x10, SeekOrigin.Begin);    // Goto the start of the un-set 0 data we set earlier and set it.
                gw.Write(dataOffset);               // Write Data Offset
                gw.Write(garcLength);               // Write total GARC Length
                gw.Write(largestFile);              // Write Largest File stat (?)

                newGARC.Seek(0, SeekOrigin.End);    // Goto the end so we can copy the filedata after the GARC headers.

                // Write in the data
                GARCdata.Seek(0, SeekOrigin.Begin); // Goto the start so we copy from the start of the filedata.
                GARCdata.CopyTo(newGARC);           // Copy the data.

                // New File is ready to be saved (memstream newGARC)
                try
                {
                    File.Delete(garcPath);  // Delete the old garc if it exists, then write our new one
                    using (FileStream file = new FileStream(garcPath, FileMode.Create, FileAccess.Write))
                        newGARC.WriteTo(file);

                    // Delete our working directory.
                    Directory.Delete(buildPath, true);

                    // We're done.
                    System.Media.SystemSounds.Exclamation.Play();
                    MessageBox.Show("Pack Successful!\n\n" + filepaths.Length + " files packed to the GARC!");
                    return true;
                }
                catch (Exception e) { MessageBox.Show("Packing Failed!\n\n" + e); return false; }
            }
        }
        public static bool garcUnpack(string garcPath, string outPath, bool skipDecompression, ProgressBar pBar1 = null)
        {
            if (!File.Exists(garcPath)) { MessageBox.Show("File does not exist"); return false; }

            // Fetch file extension (first 4 bytes) to check if it is a GARC
            using (BinaryReader br = new BinaryReader(System.IO.File.OpenRead(garcPath)))
            {
                try {
                    string s = new string(Util.Reverse(br.ReadChars(4)));
                    if (s != "GARC") { MessageBox.Show("Input file is not a .GARC"); return false; }
                    }
                catch (Exception) { MessageBox.Show("Invalid File."); return false; }
            }

            // Unpack the GARC
            GARC garc = ARC.unpackGARC(garcPath);

            // Get file path infos for exporting
            FileInfo fileInfo = new FileInfo(garcPath);

            // Initialize ProgressBar
            if (pBar1 == null) pBar1 = new ProgressBar();

            if (pBar1.InvokeRequired)
                pBar1.Invoke((MethodInvoker)delegate 
                { pBar1.Minimum = 0; pBar1.Step = 1; pBar1.Value = 0; pBar1.Maximum = (int)garc.btaf.nFiles; }
                );
            else
                { pBar1.Minimum = 0; pBar1.Step = 1; pBar1.Value = 0; pBar1.Maximum = (int)garc.btaf.nFiles; }

            using (BinaryReader br = new BinaryReader(System.IO.File.OpenRead(garcPath)))
            {
                // Create Extraction folder if it does not exist.
                if (!Directory.Exists(outPath))
                {
                    DirectoryInfo di = Directory.CreateDirectory(outPath);
                }
            
                // Pull out all the files
                for (int i = 0; i < garc.btaf.nFiles; i++)
                {
                    string ext = "bin";
                    bool compressed = false;

                    br.BaseStream.Position = garc.btaf.entries[i].start_offset + garc.data_offset;
                    try
                    {
                        byte lzss = (byte)br.PeekChar();

                        if (lzss == 0x11)
                            compressed = true;
                        else
                        {
                            ext = Util.GuessExtension(br, "bin");
                            br.BaseStream.Seek(0, SeekOrigin.Begin);
                        }
                    }
                    catch { ext = null; }

                    // Set File Name
                    string filename = i.ToString("D" + Math.Ceiling(Math.Log10(garc.btaf.nFiles)));
                    string fileout = Path.Combine(outPath,filename + "." + ext);
                    using (BinaryWriter bw = new BinaryWriter(File.OpenWrite(fileout)))
                    {
                        // Write out the data for the file
                        br.BaseStream.Position = garc.btaf.entries[i].start_offset + garc.data_offset;
                        for (int x = 0; x < garc.btaf.entries[i].length; x++)
                        {
                            bw.Write(br.ReadByte());
                        }
                    }
                    // See if decompression should be attempted.
                    #region Decompression
                    if (compressed && !skipDecompression)
                    {
                        string decout = Path.Combine(outPath,"dec_" + filename + ".bin");
                        try
                        {
                            long n = dsdecmp.Decompress(fileout,decout);
                            try { File.Delete(fileout); }
                            catch { MessageBox.Show("A compressed file could not be deleted for some reason"); }

                            // Try to detect for extension now
                            using (BinaryReader dr = new BinaryReader(System.IO.File.OpenRead(decout)))
                            {
                                ext = Util.GuessExtension(dr, "bin");
                            }

                            File.Move(decout, Path.Combine(outPath, "dec_" + filename + "." + ext));
                        }
                        catch
                        {
                            // File is really not encrypted.
                            try { File.Delete(decout); }
                            catch { MessageBox.Show("This shouldn't happen"); }
                        }
                    }
                    if (pBar1.InvokeRequired)
                        pBar1.Invoke((MethodInvoker)delegate { pBar1.PerformStep(); });
                    else
                        pBar1.PerformStep();
                    #endregion
                }
            }
            System.Media.SystemSounds.Exclamation.Play();
            MessageBox.Show("Unpack Successful!\n\n" + garc.btaf.nFiles + " files unpacked from the GARC!");
            return true;
        }
    }

    // Sub-classes
    public class Util
    {
        public static uint Reverse(uint x)
        {
            uint y = 0;
            for (int i = 0; i < 32; ++i)
            {
                y <<= 1;
                y |= (x & 1);
                x >>= 1;
            }
            return y;
        }
        public static char[] Reverse(char[] charArray)
        {
            Array.Reverse(charArray);
            return charArray;
        }
        public static string TrimFromZero(string input)
        {
            int index = input.IndexOf('\0');
            if (index != input.Length - 1)
                return input;

            return input.Substring(0, index);
        }
        public static string GuessExtension(BinaryReader br, string defaultExt)
        {
            string ext = "";
            long position = br.BaseStream.Position;
            byte[] magic = System.Text.Encoding.ASCII.GetBytes(br.ReadChars(4));
            if (magic[0] < 0x41) return defaultExt;

            // check for extensions
            {
                // check for 2char container extensions
                ushort count = BitConverter.ToUInt16(magic, 2);
                br.BaseStream.Position = 4 + 4 * count;
                if (br.ReadUInt32() == br.BaseStream.Length)
                {
                    ext += (char)magic[0];
                    ext += (char)magic[1];
                }

                // else Assume Generic 
                else
                {
                    for (int i = 0; i < magic.Length && i < 4; i++)
                    {
                        if ((magic[i] >= 'a' && magic[i] <= 'z') || (magic[i] >= 'A' && magic[i] <= 'Z')
                            || char.IsDigit((char)magic[i]))
                        {
                            ext += (char)magic[i];
                        }
                        else
                            break;
                    }
                }
            }
            // Return BaseStream position to the start.
            br.BaseStream.Position = position;

            if (ext.Length <= 1)
                return defaultExt;
            return ext;
        }
    }
    #region GARC Class & Struct
    public class ARC
    {
        public static GARC unpackGARC(string path)
        {
            GARC garc = new GARC();
            BinaryReader br = new BinaryReader(System.IO.File.OpenRead(path));

            // GARC Header
            garc.id = br.ReadChars(4);
            garc.header_size = br.ReadUInt32();
            garc.id_endian = br.ReadUInt16();
            if (garc.id_endian == 0xFEFF)
                Util.Reverse(garc.id);
            garc.constant = br.ReadUInt16();
            garc.file_size = br.ReadUInt32();

            garc.data_offset = br.ReadUInt32();
            garc.file_length = br.ReadUInt32();
            garc.lastsize = br.ReadUInt32();

            // OTAF 
            garc.otaf.id = br.ReadChars(4);
            garc.otaf.section_size = br.ReadUInt32();
            garc.otaf.nFiles = br.ReadUInt16();
            garc.otaf.padding = br.ReadUInt16();

            garc.otaf.entries = new OTAF_Entry[garc.otaf.nFiles];
            // not really needed; plus it's wrong
            for (int i = 0; i < garc.otaf.nFiles; i++)
            {
                uint val = br.ReadUInt32();
                if (garc.otaf.padding == 0xffff)
                {
                    val = Util.Reverse(val);
                }
                garc.otaf.entries[i].name = val.ToString();
            }

            // BTAF (File Allocation TaBle)
            garc.btaf.id = br.ReadChars(4);
            garc.btaf.section_size = br.ReadUInt32();
            garc.btaf.nFiles = br.ReadUInt32();
            garc.btaf.bits = br.ReadUInt32();

            garc.btaf.entries = new BTAF_Entry[garc.btaf.nFiles];
            for (int i = 0; i < garc.btaf.nFiles; i++)
            {
                garc.btaf.entries[i].start_offset = br.ReadUInt32();
                garc.btaf.entries[i].end_offset = br.ReadUInt32();
                garc.btaf.entries[i].length = br.ReadUInt32();
            }

            // BMIF
            garc.gmif.id = br.ReadChars(4);
            garc.gmif.section_size = br.ReadUInt32();
            garc.gmif.data_size = br.ReadUInt32();

            // Files data

            br.Close();
            return garc;
        }
    }
    public struct GARC
    {
        public char[] id;           // Always GARC = 0x4E415243
        public UInt32 header_size;  // Always 0x001C
        public UInt16 id_endian;    // 0xFFFE
        public UInt16 constant;     // Always 0x0400 chunk count
        public UInt32 file_size;

        public UInt32 data_offset;
        public UInt32 file_length;
        public UInt32 lastsize;

        public OTAF otaf;
        public BTAF btaf;
        public GMIF gmif;
    }
    public struct OTAF
    {
        public char[] id;
        public UInt32 section_size;
        public UInt16 nFiles;
        public UInt16 padding;

        public OTAF_Entry[] entries;
    }
    public struct OTAF_Entry
    {
        public string name;
    }
    public struct BTAF
    {
        public char[] id;
        public UInt32 section_size;
        public UInt32 nFiles;
        public BTAF_Entry[] entries;
        public UInt32 bits;
    }
    public struct BTAF_Entry
    {
        public UInt32 start_offset;
        public UInt32 end_offset;
        public UInt32 length;
    }
    public struct GMIF
    {
        public char[] id;
        public UInt32 section_size;
        public UInt32 data_size;
    }
    #endregion
    #endregion
    #region dsdecmp Classes
    public class dsdecmp // LZ11 (de)compression
    {
        public static long Decompress(string infile, string outfile)
        {
            // make sure the output directory exists
            string outDirectory = Path.GetDirectoryName(outfile);
            if (!Directory.Exists(outDirectory))
                Directory.CreateDirectory(outDirectory);
            // open the two given files, and delegate to the format-specific code.
            using (FileStream inStream = new FileStream(infile, FileMode.Open),
                             outStream = new FileStream(outfile, FileMode.Create))
            {
                return Decompress(inStream, inStream.Length, outStream);
            }
        }
        /// <summary>
        /// Decompresses the given stream, writing the decompressed data to the given output stream.
        /// Assumes <code>Supports(instream)</code> returns <code>true</code>.
        /// After this call, the input stream will be positioned at the end of the compressed stream,
        /// or at the initial position + <code>inLength</code>, whichever comes first.
        /// </summary>
        /// <param name="instream">The stream to decompress. At the end of this method, the position
        /// of this stream is directly after the compressed data.</param>
        /// <param name="inLength">The length of the input data. Not necessarily all of the
        /// input data may be read (if there is padding, for example), however never more than
        /// this number of bytes is read from the input stream.</param>
        /// <param name="outstream">The stream to write the decompressed data to.</param>
        /// <returns>The length of the output data.</returns>
        /// <exception cref="NotEnoughDataException">When the given length of the input data
        /// is not enough to properly decompress the input.</exception>
        public static long Decompress(Stream instream, long inLength, Stream outstream)
        {
            #region Format definition in NDSTEK style
            /*  Data header (32bit)
                  Bit 0-3   Reserved
                  Bit 4-7   Compressed type (must be 1 for LZ77)
                  Bit 8-31  Size of decompressed data. if 0, the next 4 bytes are decompressed length
                Repeat below. Each Flag Byte followed by eight Blocks.
                Flag data (8bit)
                  Bit 0-7   Type Flags for next 8 Blocks, MSB first
                Block Type 0 - Uncompressed - Copy 1 Byte from Source to Dest
                  Bit 0-7   One data byte to be copied to dest
                Block Type 1 - Compressed - Copy LEN Bytes from Dest-Disp-1 to Dest
                    If Reserved is 0: - Default
                      Bit 0-3   Disp MSBs
                      Bit 4-7   LEN - 3
                      Bit 8-15  Disp LSBs
                    If Reserved is 1: - Higher compression rates for files with (lots of) long repetitions
                      Bit 4-7   Indicator
                        If Indicator > 1:
                            Bit 0-3    Disp MSBs
                            Bit 4-7    LEN - 1 (same bits as Indicator)
                            Bit 8-15   Disp LSBs
                        If Indicator is 1: A(B CD E)(F GH)
                            Bit 0-3     (LEN - 0x111) MSBs
                            Bit 4-7     Indicator; unused
                            Bit 8-15    (LEN- 0x111) 'middle'-SBs
                            Bit 16-19   Disp MSBs
                            Bit 20-23   (LEN - 0x111) LSBs
                            Bit 24-31   Disp LSBs
                        If Indicator is 0:
                            Bit 0-3     (LEN - 0x11) MSBs
                            Bit 4-7     Indicator; unused
                            Bit 8-11    Disp MSBs
                            Bit 12-15   (LEN - 0x11) LSBs
                            Bit 16-23   Disp LSBs
             */
            #endregion

            long readBytes = 0;

            byte type = (byte)instream.ReadByte();
            if (type != 0x11)
                throw new InvalidDataException("The provided stream is not a valid LZ-0x11 "
                            + "compressed stream (invalid type 0x" + type.ToString("X") + ")");
            byte[] sizeBytes = new byte[3];
            instream.Read(sizeBytes, 0, 3);
            int decompressedSize = IOUtils.ToNDSu24(sizeBytes, 0);
            readBytes += 4;
            if (decompressedSize == 0)
            {
                sizeBytes = new byte[4];
                instream.Read(sizeBytes, 0, 4);
                decompressedSize = IOUtils.ToNDSs32(sizeBytes, 0);
                readBytes += 4;
            }

            // the maximum 'DISP-1' is still 0xFFF.
            int bufferLength = 0x1000;
            byte[] buffer = new byte[bufferLength];
            int bufferOffset = 0;

            int currentOutSize = 0;
            int flags = 0, mask = 1;
            while (currentOutSize < decompressedSize)
            {
                // (throws when requested new flags byte is not available)
                #region Update the mask. If all flag bits have been read, get a new set.
                // the current mask is the mask used in the previous run. So if it masks the
                // last flag bit, get a new flags byte.
                if (mask == 1)
                {
                    if (readBytes >= inLength)
                        throw new NotEnoughDataException(currentOutSize, decompressedSize);
                    flags = instream.ReadByte(); readBytes++;
                    if (flags < 0)
                        throw new StreamTooShortException();
                    mask = 0x80;
                }
                else
                {
                    mask >>= 1;
                }
                #endregion

                // bit = 1 <=> compressed.
                if ((flags & mask) > 0)
                {
                    // (throws when not enough bytes are available)
                    #region Get length and displacement('disp') values from next 2, 3 or 4 bytes

                    // read the first byte first, which also signals the size of the compressed block
                    if (readBytes >= inLength)
                        throw new NotEnoughDataException(currentOutSize, decompressedSize);
                    int byte1 = instream.ReadByte(); readBytes++;
                    if (byte1 < 0)
                        throw new StreamTooShortException();

                    int length = byte1 >> 4;
                    int disp = -1;
                    if (length == 0)
                    {
                        #region case 0; 0(B C)(D EF) + (0x11)(0x1) = (LEN)(DISP)

                        // case 0:
                        // data = AB CD EF (with A=0)
                        // LEN = ABC + 0x11 == BC + 0x11
                        // DISP = DEF + 1

                        // we need two more bytes available
                        if (readBytes + 1 >= inLength)
                            throw new NotEnoughDataException(currentOutSize, decompressedSize);
                        int byte2 = instream.ReadByte(); readBytes++;
                        int byte3 = instream.ReadByte(); readBytes++;
                        if (byte3 < 0)
                            throw new StreamTooShortException();

                        length = (((byte1 & 0x0F) << 4) | (byte2 >> 4)) + 0x11;
                        disp = (((byte2 & 0x0F) << 8) | byte3) + 0x1;

                        #endregion
                    }
                    else if (length == 1)
                    {
                        #region case 1: 1(B CD E)(F GH) + (0x111)(0x1) = (LEN)(DISP)

                        // case 1:
                        // data = AB CD EF GH (with A=1)
                        // LEN = BCDE + 0x111
                        // DISP = FGH + 1

                        // we need three more bytes available
                        if (readBytes + 2 >= inLength)
                            throw new NotEnoughDataException(currentOutSize, decompressedSize);
                        int byte2 = instream.ReadByte(); readBytes++;
                        int byte3 = instream.ReadByte(); readBytes++;
                        int byte4 = instream.ReadByte(); readBytes++;
                        if (byte4 < 0)
                            throw new StreamTooShortException();

                        length = (((byte1 & 0x0F) << 12) | (byte2 << 4) | (byte3 >> 4)) + 0x111;
                        disp = (((byte3 & 0x0F) << 8) | byte4) + 0x1;

                        #endregion
                    }
                    else
                    {
                        #region case > 1: (A)(B CD) + (0x1)(0x1) = (LEN)(DISP)

                        // case other:
                        // data = AB CD
                        // LEN = A + 1
                        // DISP = BCD + 1

                        // we need only one more byte available
                        if (readBytes >= inLength)
                            throw new NotEnoughDataException(currentOutSize, decompressedSize);
                        int byte2 = instream.ReadByte(); readBytes++;
                        if (byte2 < 0)
                            throw new StreamTooShortException();

                        length = ((byte1 & 0xF0) >> 4) + 0x1;
                        disp = (((byte1 & 0x0F) << 8) | byte2) + 0x1;

                        #endregion
                    }

                    if (disp > currentOutSize)
                        throw new InvalidDataException("Cannot go back more than already written. "
                                + "DISP = " + disp + ", #written bytes = 0x" + currentOutSize.ToString("X")
                                + " before 0x" + instream.Position.ToString("X") + " with indicator 0x"
                                + (byte1 >> 4).ToString("X"));
                    #endregion

                    int bufIdx = bufferOffset + bufferLength - disp;
                    for (int i = 0; i < length; i++)
                    {
                        byte next = buffer[bufIdx % bufferLength];
                        bufIdx++;
                        outstream.WriteByte(next);
                        buffer[bufferOffset] = next;
                        bufferOffset = (bufferOffset + 1) % bufferLength;
                    }
                    currentOutSize += length;
                }
                else
                {
                    if (readBytes >= inLength)
                        throw new NotEnoughDataException(currentOutSize, decompressedSize);
                    int next = instream.ReadByte(); readBytes++;
                    if (next < 0)
                        throw new StreamTooShortException();

                    outstream.WriteByte((byte)next); currentOutSize++;
                    buffer[bufferOffset] = (byte)next;
                    bufferOffset = (bufferOffset + 1) % bufferLength;
                }
            }

            if (readBytes < inLength)
            {
                // the input may be 4-byte aligned.
                if ((readBytes ^ (readBytes & 3)) + 4 < inLength)
                    throw new TooMuchInputException(readBytes, inLength);
            }

            return decompressedSize;
        }

        public static int Compress(string infile, string outfile)
        {
            // make sure the output directory exists
            string outDirectory = Path.GetDirectoryName(outfile);
            if (!Directory.Exists(outDirectory))
                Directory.CreateDirectory(outDirectory);
            // open the proper Streams, and delegate to the format-specific code.
            using (FileStream inStream = File.Open(infile, FileMode.Open),
                             outStream = File.Create(outfile))
            {
                return dsdecmp.Compress(inStream, inStream.Length, outStream, true);
            }
        }
        #region Original compression method
        /// <summary>
        /// Compresses the input using the 'original', unoptimized compression algorithm.
        /// This algorithm should yield files that are the same as those found in the games.
        /// (delegates to the optimized method if LookAhead is set)
        /// </summary>
        public unsafe static int Compress(Stream instream, long inLength, Stream outstream, bool original)
        {
            // make sure the decompressed size fits in 3 bytes.
            // There should be room for four bytes, however I'm not 100% sure if that can be used
            // in every game, as it may not be a built-in function.
            if (inLength > 0xFFFFFF)
                throw new InputTooLargeException();

            // use the other method if lookahead is enabled
            if (!original)
            {
                return CompressWithLA(instream, inLength, outstream);
            }

            // save the input data in an array to prevent having to go back and forth in a file
            byte[] indata = new byte[inLength];
            int numReadBytes = instream.Read(indata, 0, (int)inLength);
            if (numReadBytes != inLength)
                throw new StreamTooShortException();

            // write the compression header first
            outstream.WriteByte(0x11);
            outstream.WriteByte((byte)(inLength & 0xFF));
            outstream.WriteByte((byte)((inLength >> 8) & 0xFF));
            outstream.WriteByte((byte)((inLength >> 16) & 0xFF));

            int compressedLength = 4;

            fixed (byte* instart = &indata[0])
            {
                // we do need to buffer the output, as the first byte indicates which blocks are compressed.
                // this version does not use a look-ahead, so we do not need to buffer more than 8 blocks at a time.
                // (a block is at most 4 bytes long)
                byte[] outbuffer = new byte[8 * 4 + 1];
                outbuffer[0] = 0;
                int bufferlength = 1, bufferedBlocks = 0;
                int readBytes = 0;
                while (readBytes < inLength)
                {
                    #region If 8 blocks are bufferd, write them and reset the buffer
                    // we can only buffer 8 blocks at a time.
                    if (bufferedBlocks == 8)
                    {
                        outstream.Write(outbuffer, 0, bufferlength);
                        compressedLength += bufferlength;
                        // reset the buffer
                        outbuffer[0] = 0;
                        bufferlength = 1;
                        bufferedBlocks = 0;
                    }
                    #endregion

                    // determine if we're dealing with a compressed or raw block.
                    // it is a compressed block when the next 3 or more bytes can be copied from
                    // somewhere in the set of already compressed bytes.
                    int disp;
                    int oldLength = Math.Min(readBytes, 0x1000);
                    int length = LZUtil.GetOccurrenceLength(instart + readBytes, (int)Math.Min(inLength - readBytes, 0x10110),
                                                          instart + readBytes - oldLength, oldLength, out disp);

                    // length not 3 or more? next byte is raw data
                    if (length < 3)
                    {
                        outbuffer[bufferlength++] = *(instart + (readBytes++));
                    }
                    else
                    {
                        // 3 or more bytes can be copied? next (length) bytes will be compressed into 2 bytes
                        readBytes += length;

                        // mark the next block as compressed
                        outbuffer[0] |= (byte)(1 << (7 - bufferedBlocks));

                        if (length > 0x110)
                        {
                            // case 1: 1(B CD E)(F GH) + (0x111)(0x1) = (LEN)(DISP)
                            outbuffer[bufferlength] = 0x10;
                            outbuffer[bufferlength] |= (byte)(((length - 0x111) >> 12) & 0x0F);
                            bufferlength++;
                            outbuffer[bufferlength] = (byte)(((length - 0x111) >> 4) & 0xFF);
                            bufferlength++;
                            outbuffer[bufferlength] = (byte)(((length - 0x111) << 4) & 0xF0);
                        }
                        else if (length > 0x10)
                        {
                            // case 0; 0(B C)(D EF) + (0x11)(0x1) = (LEN)(DISP)
                            outbuffer[bufferlength] = 0x00;
                            outbuffer[bufferlength] |= (byte)(((length - 0x111) >> 4) & 0x0F);
                            bufferlength++;
                            outbuffer[bufferlength] = (byte)(((length - 0x111) << 4) & 0xF0);
                        }
                        else
                        {
                            // case > 1: (A)(B CD) + (0x1)(0x1) = (LEN)(DISP)
                            outbuffer[bufferlength] = (byte)(((length - 1) << 4) & 0xF0);
                        }
                        // the last 1.5 bytes are always the disp
                        outbuffer[bufferlength] |= (byte)(((disp - 1) >> 8) & 0x0F);
                        bufferlength++;
                        outbuffer[bufferlength] = (byte)((disp - 1) & 0xFF);
                        bufferlength++;
                    }
                    bufferedBlocks++;
                }

                // copy the remaining blocks to the output
                if (bufferedBlocks > 0)
                {
                    outstream.Write(outbuffer, 0, bufferlength);
                    compressedLength += bufferlength;
                    /*/ make the compressed file 4-byte aligned.
                    while ((compressedLength % 4) != 0)
                    {
                        outstream.WriteByte(0);
                        compressedLength++;
                    }/**/
                }
            }

            return compressedLength;
        }
        #region Dynamic Programming compression method
        /// <summary>
        /// Variation of the original compression method, making use of Dynamic Programming to 'look ahead'
        /// and determine the optimal 'length' values for the compressed blocks. Is not 100% optimal,
        /// as the flag-bytes are not taken into account.
        /// </summary>
        private unsafe static int CompressWithLA(Stream instream, long inLength, Stream outstream)
        {
            // save the input data in an array to prevent having to go back and forth in a file
            byte[] indata = new byte[inLength];
            int numReadBytes = instream.Read(indata, 0, (int)inLength);
            if (numReadBytes != inLength)
                throw new StreamTooShortException();

            // write the compression header first
            outstream.WriteByte(0x11);
            outstream.WriteByte((byte)(inLength & 0xFF));
            outstream.WriteByte((byte)((inLength >> 8) & 0xFF));
            outstream.WriteByte((byte)((inLength >> 16) & 0xFF));

            int compressedLength = 4;

            fixed (byte* instart = &indata[0])
            {
                // we do need to buffer the output, as the first byte indicates which blocks are compressed.
                // this version does not use a look-ahead, so we do not need to buffer more than 8 blocks at a time.
                // blocks are at most 4 bytes long.
                byte[] outbuffer = new byte[8 * 4 + 1];
                outbuffer[0] = 0;
                int bufferlength = 1, bufferedBlocks = 0;
                int readBytes = 0;

                // get the optimal choices for len and disp
                int[] lengths, disps;
                GetOptimalCompressionLengths(instart, indata.Length, out lengths, out disps);
                while (readBytes < inLength)
                {
                    // we can only buffer 8 blocks at a time.
                    if (bufferedBlocks == 8)
                    {
                        outstream.Write(outbuffer, 0, bufferlength);
                        compressedLength += bufferlength;
                        // reset the buffer
                        outbuffer[0] = 0;
                        bufferlength = 1;
                        bufferedBlocks = 0;
                    }


                    if (lengths[readBytes] == 1)
                    {
                        outbuffer[bufferlength++] = *(instart + (readBytes++));
                    }
                    else
                    {
                        // mark the next block as compressed
                        outbuffer[0] |= (byte)(1 << (7 - bufferedBlocks));

                        if (lengths[readBytes] > 0x110)
                        {
                            // case 1: 1(B CD E)(F GH) + (0x111)(0x1) = (LEN)(DISP)
                            outbuffer[bufferlength] = 0x10;
                            outbuffer[bufferlength] |= (byte)(((lengths[readBytes] - 0x111) >> 12) & 0x0F);
                            bufferlength++;
                            outbuffer[bufferlength] = (byte)(((lengths[readBytes] - 0x111) >> 4) & 0xFF);
                            bufferlength++;
                            outbuffer[bufferlength] = (byte)(((lengths[readBytes] - 0x111) << 4) & 0xF0);
                        }
                        else if (lengths[readBytes] > 0x10)
                        {
                            // case 0; 0(B C)(D EF) + (0x11)(0x1) = (LEN)(DISP)
                            outbuffer[bufferlength] = 0x00;
                            outbuffer[bufferlength] |= (byte)(((lengths[readBytes] - 0x111) >> 4) & 0x0F);
                            bufferlength++;
                            outbuffer[bufferlength] = (byte)(((lengths[readBytes] - 0x111) << 4) & 0xF0);
                        }
                        else
                        {
                            // case > 1: (A)(B CD) + (0x1)(0x1) = (LEN)(DISP)
                            outbuffer[bufferlength] = (byte)(((lengths[readBytes] - 1) << 4) & 0xF0);
                        }
                        // the last 1.5 bytes are always the disp
                        outbuffer[bufferlength] |= (byte)(((disps[readBytes] - 1) >> 8) & 0x0F);
                        bufferlength++;
                        outbuffer[bufferlength] = (byte)((disps[readBytes] - 1) & 0xFF);
                        bufferlength++;

                        readBytes += lengths[readBytes];
                    }


                    bufferedBlocks++;
                }

                // copy the remaining blocks to the output
                if (bufferedBlocks > 0)
                {
                    outstream.Write(outbuffer, 0, bufferlength);
                    compressedLength += bufferlength;
                    /*/ make the compressed file 4-byte aligned.
                    while ((compressedLength % 4) != 0)
                    {
                        outstream.WriteByte(0);
                        compressedLength++;
                    }/**/
                }
            }

            return compressedLength;
        }
        #endregion

        #region DP compression helper method; GetOptimalCompressionLengths
        /// <summary>
        /// Gets the optimal compression lengths for each start of a compressed block using Dynamic Programming.
        /// This takes O(n^2) time, although in practice it will often be O(n^3) since one of the constants is 0x10110
        /// (the maximum length of a compressed block)
        /// </summary>
        /// <param name="indata">The data to compress.</param>
        /// <param name="inLength">The length of the data to compress.</param>
        /// <param name="lengths">The optimal 'length' of the compressed blocks. For each byte in the input data,
        /// this value is the optimal 'length' value. If it is 1, the block should not be compressed.</param>
        /// <param name="disps">The 'disp' values of the compressed blocks. May be 0, in which case the
        /// corresponding length will never be anything other than 1.</param>
        private unsafe static void GetOptimalCompressionLengths(byte* indata, int inLength, out int[] lengths, out int[] disps)
        {
            lengths = new int[inLength];
            disps = new int[inLength];
            int[] minLengths = new int[inLength];

            for (int i = inLength - 1; i >= 0; i--)
            {
                // first get the compression length when the next byte is not compressed
                minLengths[i] = int.MaxValue;
                lengths[i] = 1;
                if (i + 1 >= inLength)
                    minLengths[i] = 1;
                else
                    minLengths[i] = 1 + minLengths[i + 1];
                // then the optimal compressed length
                int oldLength = Math.Min(0x1000, i);
                // get the appropriate disp while at it. Takes at most O(n) time if oldLength is considered O(n) and 0x10110 constant.
                // however since a lot of files will not be larger than 0x10110, this will often take ~O(n^2) time.
                // be sure to bound the input length with 0x10110, as that's the maximum length for LZ-11 compressed blocks.
                int maxLen = LZUtil.GetOccurrenceLength(indata + i, Math.Min(inLength - i, 0x10110),
                                                 indata + i - oldLength, oldLength, out disps[i]);
                if (disps[i] > i)
                    throw new Exception("disp is too large");
                for (int j = 3; j <= maxLen; j++)
                {
                    int blocklen;
                    if (j > 0x110)
                        blocklen = 4;
                    else if (j > 0x10)
                        blocklen = 3;
                    else
                        blocklen = 2;
                    int newCompLen;
                    if (i + j >= inLength)
                        newCompLen = blocklen;
                    else
                        newCompLen = blocklen + minLengths[i + j];
                    if (newCompLen < minLengths[i])
                    {
                        lengths[i] = j;
                        minLengths[i] = newCompLen;
                    }
                }
            }

            // we could optimize this further to also optimize it with regard to the flag-bytes, but that would require 8 times
            // more space and time (one for each position in the block) for only a potentially tiny increase in compression ratio.
        }
        #endregion
        #endregion
    }
    #region Exceptions
    /// <summary>
    /// An exception indicating that the file cannot be compressed, because the decompressed size
    /// cannot be represented in the current compression format.
    /// </summary>
    public class InputTooLargeException : Exception
    {
        /// <summary>
        /// Creates a new exception that indicates that the input is too big to be compressed.
        /// </summary>
        public InputTooLargeException()
            : base("The compression ratio is not high enough to fit the input "
            + "in a single compressed file.") { }
    }
    /// <summary>
    /// An exception that is thrown by the decompression functions when there
    /// is not enough data available in order to properly decompress the input.
    /// </summary>
    public class NotEnoughDataException : IOException
    {
        private long currentOutSize;
        private long totalOutSize;
        /// <summary>
        /// Gets the actual number of written bytes.
        /// </summary>
        public long WrittenLength { get { return this.currentOutSize; } }
        /// <summary>
        /// Gets the number of bytes that was supposed to be written.
        /// </summary>
        public long DesiredLength { get { return this.totalOutSize; } }

        /// <summary>
        /// Creates a new NotEnoughDataException.
        /// </summary>
        /// <param name="currentOutSize">The actual number of written bytes.</param>
        /// <param name="totalOutSize">The desired number of written bytes.</param>
        public NotEnoughDataException(long currentOutSize, long totalOutSize)
            : base("Not enough data availble; 0x" + currentOutSize.ToString("X")
                + " of " + (totalOutSize < 0 ? "???" : ("0x" + totalOutSize.ToString("X")))
                + " bytes written.")
        {
            this.currentOutSize = currentOutSize;
            this.totalOutSize = totalOutSize;
        }
    }
    /// <summary>
    /// An exception thrown by the compression or decompression function, indicating that the
    /// given input length was too large for the given input stream.
    /// </summary>
    public class StreamTooShortException : EndOfStreamException
    {
        /// <summary>
        /// Creates a new exception that indicates that the stream was shorter than the given input length.
        /// </summary>
        public StreamTooShortException()
            : base("The end of the stream was reached "
                 + "before the given amout of data was read.")
        { }
    }
    /// <summary>
    /// An exception indication that the input has more data than required in order
    /// to decompress it. This may indicate that more sub-files are present in the file.
    /// </summary>
    public class TooMuchInputException : Exception
    {
        /// <summary>
        /// Gets the number of bytes read by the decompressed to decompress the stream.
        /// </summary>
        public long ReadBytes { get; private set; }

        /// <summary>
        /// Creates a new exception indicating that the input has more data than necessary for
        /// decompressing th stream. It may indicate that other data is present after the compressed
        /// stream.
        /// </summary>
        /// <param name="readBytes">The number of bytes read by the decompressor.</param>
        /// <param name="totLength">The indicated length of the input stream.</param>
        public TooMuchInputException(long readBytes, long totLength)
            : base("The input contains more data than necessary. Only used 0x"
            + readBytes.ToString("X") + " of 0x" + totLength.ToString("X") + " bytes")
        {
            this.ReadBytes = readBytes;
        }
    }
    #endregion
    #region Supplementary
    /// <summary>
    /// Class for I/O-related utility methods.
    /// </summary>
    public static class LZUtil
    {
        /// <summary>
        /// Determine the maximum size of a LZ-compressed block starting at newPtr, using the already compressed data
        /// starting at oldPtr. Takes O(inLength * oldLength) = O(n^2) time.
        /// </summary>
        /// <param name="newPtr">The start of the data that needs to be compressed.</param>
        /// <param name="newLength">The number of bytes that still need to be compressed.
        /// (or: the maximum number of bytes that _may_ be compressed into one block)</param>
        /// <param name="oldPtr">The start of the raw file.</param>
        /// <param name="oldLength">The number of bytes already compressed.</param>
        /// <param name="disp">The offset of the start of the longest block to refer to.</param>
        /// <param name="minDisp">The minimum allowed value for 'disp'.</param>
        /// <returns>The length of the longest sequence of bytes that can be copied from the already decompressed data.</returns>
        public static unsafe int GetOccurrenceLength(byte* newPtr, int newLength, byte* oldPtr, int oldLength, out int disp, int minDisp = 1)
        {
            disp = 0;
            if (newLength == 0)
                return 0;
            int maxLength = 0;
            // try every possible 'disp' value (disp = oldLength - i)
            for (int i = 0; i < oldLength - minDisp; i++)
            {
                // work from the start of the old data to the end, to mimic the original implementation's behaviour
                // (and going from start to end or from end to start does not influence the compression ratio anyway)
                byte* currentOldStart = oldPtr + i;
                int currentLength = 0;
                // determine the length we can copy if we go back (oldLength - i) bytes
                // always check the next 'newLength' bytes, and not just the available 'old' bytes,
                // as the copied data can also originate from what we're currently trying to compress.
                for (int j = 0; j < newLength; j++)
                {
                    // stop when the bytes are no longer the same
                    if (*(currentOldStart + j) != *(newPtr + j))
                        break;
                    currentLength++;
                }

                // update the optimal value
                if (currentLength > maxLength)
                {
                    maxLength = currentLength;
                    disp = oldLength - i;

                    // if we cannot do better anyway, stop trying.
                    if (maxLength == newLength)
                        break;
                }
            }
            return maxLength;
        }
    }
    public static class IOUtils
    {
        #region byte[] <-> (u)int
        /// <summary>
        /// Returns a 4-byte unsigned integer as used on the NDS converted from four bytes
        /// at a specified position in a byte array.
        /// </summary>
        /// <param name="buffer">The source of the data.</param>
        /// <param name="offset">The location of the data in the source.</param>
        /// <returns>The indicated 4 bytes converted to uint</returns>
        public static uint ToNDSu32(byte[] buffer, int offset)
        {
            return (uint)(buffer[offset]
                        | (buffer[offset + 1] << 8)
                        | (buffer[offset + 2] << 16)
                        | (buffer[offset + 3] << 24));
        }

        /// <summary>
        /// Returns a 4-byte signed integer as used on the NDS converted from four bytes
        /// at a specified position in a byte array.
        /// </summary>
        /// <param name="buffer">The source of the data.</param>
        /// <param name="offset">The location of the data in the source.</param>
        /// <returns>The indicated 4 bytes converted to int</returns>
        public static int ToNDSs32(byte[] buffer, int offset)
        {
            return (int)(buffer[offset]
                        | (buffer[offset + 1] << 8)
                        | (buffer[offset + 2] << 16)
                        | (buffer[offset + 3] << 24));
        }

        /// <summary>
        /// Converts a u32 value into a sequence of bytes that would make ToNDSu32 return
        /// the given input value.
        /// </summary>
        public static byte[] FromNDSu32(uint value)
        {
            return new byte[] {
                (byte)(value & 0xFF),
                (byte)((value >> 8) & 0xFF),
                (byte)((value >> 16) & 0xFF),
                (byte)((value >> 24) & 0xFF)
            };
        }

        /// <summary>
        /// Returns a 3-byte integer as used in the built-in compression
        /// formats in the DS, convrted from three bytes at a specified position in a byte array,
        /// </summary>
        /// <param name="buffer">The source of the data.</param>
        /// <param name="offset">The location of the data in the source.</param>
        /// <returns>The indicated 3 bytes converted to an integer.</returns>
        public static int ToNDSu24(byte[] buffer, int offset)
        {
            return (int)(buffer[offset]
                        | (buffer[offset + 1] << 8)
                        | (buffer[offset + 2] << 16));
        }
        #endregion
    }
    #endregion
    #endregion
}
