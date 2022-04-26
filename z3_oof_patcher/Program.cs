namespace z3_oof_patcher
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var romFilename = "";
            var sampleFilename = "";
            var outputFilename = "";
            var archipelago = false;

            for (var i = 0; i < args.Length - 1; i++)
            {
                var arg = args[i];
                switch (arg)
                {
                    case "--rom":
                        romFilename = args[i + 1];
                        break;
                    case "--brr":
                        sampleFilename = args[i + 1];
                        break;
                    case "--output":
                        outputFilename = args[i + 1];
                        break;
                    default:
                        if (arg.StartsWith("--"))
                        {
                            throw new ProgramException($"Unrecognized parameter: {arg}");
                        }
                        break;
                }
                
            }

            //foreach (var arg in args)
            //{
            //    if (arg == "--ap")
            //        archipelago = true;
            //}

            try
            {
                //Ease of use feature: patch the first (presumably only) .sfc file in the directory if there are no arguments
                if (romFilename == string.Empty)
                    romFilename = FindRomFile();

                //If a sample isn't specified, try to use a default
                if (sampleFilename == string.Empty)
                    sampleFilename = "default.brr";

                var romFile = GetRomFile(romFilename);
                var sampleFile = GetSampleFile(sampleFilename);

                if (outputFilename == string.Empty)
                    outputFilename = "patched_" + romFilename;

                ApplyPatch(romFile, sampleFile, outputFilename, archipelago);
            }
            catch (Exception e)
            {
                if (e is not ProgramException)
                    throw;
                while (e != null)
                {
                    if (e is ProgramException)
                        Console.WriteLine("Error: " + e.Message);
                    else
                    {
                        Console.WriteLine($"{e.GetType()}: " + e.Message);
                        Console.WriteLine(e.StackTrace);
                    }
                    e = e.InnerException;
                }
            }
        }

        private static string FindRomFile()
        {
            var romFilename = "";

            var files = Directory.GetFiles("./");
            var firstRomFile = files.FirstOrDefault(x => x.EndsWith(".sfc"));

            if (firstRomFile == null)
                throw new ProgramException("No .sfc file found in current directory");

            return Path.GetFileName(firstRomFile);
        }

        private static byte[] GetRomFile(string romFilename)
        {
            //Try to open the file
            byte[] bytes;
            try
            {
                var fileStream = File.OpenRead($"./{romFilename}");
                using var ms = new MemoryStream();
                fileStream.CopyTo(ms);
                bytes = ms.ToArray();
            }
            catch (Exception e)
            {
                if (e is ArgumentException or PathTooLongException or DirectoryNotFoundException or UnauthorizedAccessException or FileNotFoundException or NotSupportedException or IOException)
                    throw new ProgramException($"Could not open rom file: '{romFilename}'", e);
                throw;
            }

            //Ensure it's the right file size
            if (bytes.Length < 2097152)
                throw new ProgramException("Rom must be expanded to 2mb");

            //Check the header to ensure it's the right ROM
            var expectedHeader = Convert.FromHexString("A48FAAC17E6BADE002D006AF56F37ED017AF57F37EF0039CE002A90C854BA92AA61BF002A91485116BA90C854BA92AA6");
            var offset = 0xFFB0;
            foreach (var b in expectedHeader)
            {
                if (bytes[offset] != b)
                    throw new ProgramException("Rom header does not match Japanese v1.0 version");
                offset++;
            }

            //All good!
            return bytes;
        }

        private static byte[] GetSampleFile(string sampleFilename)
        {
            //Try to open the file
            byte[] bytes;
            try
            {
                var fileStream = File.OpenRead($"./{sampleFilename}");
                using var ms = new MemoryStream();
                fileStream.CopyTo(ms);
                bytes = ms.ToArray();
            }
            catch (Exception e)
            {
                if (e is ArgumentException or PathTooLongException or DirectoryNotFoundException or UnauthorizedAccessException or FileNotFoundException or NotSupportedException or IOException)
                    throw new ProgramException($"Could not open sample file: '{sampleFilename}'", e);
                throw;
            }

            //Ensure it's the right file size
            if (bytes.Length > 0xA71)
                throw new ProgramException("Sample cannot exceed 2672 bytes");

            return bytes;
        }

        private static void ApplyPatch(byte[] romFile, byte[] sampleFile, string outputFilename, bool archipelago)
        {
            var patches = GetPatches(sampleFile, archipelago);

            foreach (var patch in patches)
            {
                var offset = patch.offset;
                foreach (var b in patch.bytes)
                {
                    romFile[offset] = b;
                    offset++;
                }
            }

            File.WriteAllBytes(outputFilename, romFile);
            Console.WriteLine("Patch successful.");
        }
        private static List<Patch> GetPatches(byte[] customSample, bool archipelago)
        {
            var patches = new List<Patch>();

            var h = customSample.Length.ToString("X4");
            var XXXX = string.Join("", h[2], h[3], h[0], h[1]);

            //Credit to witch princess kan on alttpr discord for an improved method over what I was doing
            //The below patches are based on their code

            //Jump execution from the SPC load routine to new code
            patches.Add(new Patch(0x8CF, "5C008025"));

            ////Presumably ALTTPR
            //if (!archipelago)
            //{
                //Change the pointer for instrument $9 in SPC memory to point to the new data we'll be inserting:
                patches.Add(new Patch(0xC806C, "88310000"));

                //Insert a sigil so we can branch on it later; this overwrites unused data
                patches.Add(new Patch(0xCFB18, "BEBE"));

                //Change the "oof" sound effect to use instrument $9:
                patches.Add(new Patch(0xD1BF5, "09"));

                //Correct pitch shift value:
                patches.Add(new Patch(0xD1BF8, "B6"));

                //Modify parameters of instrument $9
                //(I don't actually understand this part, they're just magic values to me)
                patches.Add(new Patch(0xD1C55, "7F7F00101A00007F01"));

                //Hook from SPC load routine:
                // * Check for the read of the sigil
                // * Once we find it, change the SPC load routine's data pointer to read from the location containing the new sample
                // * Note: XXXX in the string below is a placeholder for the number of bytes in the .brr sample (little endian)
                // * Another sigil "$EBEB" is inserted at the end of the data
                // * When the second sigil is read, we know we're done inserting our data so we can change the data pointer back
                // * Effect: The new data gets loaded into SPC memory without having to relocate the SPC load routine
                var byteStr = $"B700C8C8C9BEBEF009C9EBEBF01B5CD38800A2{XXXX}A980258501A93A808500A00000A988315CD88800A9801964008501A2AE00A01A7B5CD48800";
                patches.Add(new Patch(0x128000, byteStr));
            //}
            //else
            //{
            //    //Offsets need to change to accommodate archipelago roms; otherwise same as the above

            //    //We actually can't use $3188 in SPC memory because it's in use by something else
            //    //Have to use the smaller buffer from $BAA0 to $BFF0 instead
            //    patches.Add(new Patch(0x1A006C, "A0BA0000"));
            //    patches.Add(new Patch(0x1A7B18, "BEBE"));
            //    patches.Add(new Patch(0x1A9C4E, "09"));
            //    patches.Add(new Patch(0x1A9C51, "B6"));
            //    patches.Add(new Patch(0x1A9CAE, "7F7F00101A00007F01"));
                
            //    var byteStr = $"B700C8C8C9BEBEF009C9EBEBF01B5CD38800A2{XXXX}A980258501A93A808500A00000A9A0BA5CD88800A9801964008501A2AE00A01A7B5CD48800";
            //    patches.Add(new Patch(0x128000, byteStr));
            //}

            //The new sample data
            //(We need to insert the second sigil at the end)
            patches.Add(new Patch(0x12803A, Convert.ToHexString(customSample) + "EBEB"));

            return patches;
        }
    }

    internal class Patch
    {
        internal readonly int offset;
        internal readonly byte[] bytes;

        internal Patch(int offset, string byteStr)
        {
            this.offset = offset;
            this.bytes = ParseByteString(byteStr);
        }

        private static byte[] ParseByteString(string byteStr)
        {
            try
            {
                var bytes = Convert.FromHexString(byteStr);
                return bytes;
            }
            catch (FormatException e)
            {
                throw new ProgramException($"Invalid hex string: {byteStr}", e);
            }
        }
    }

    internal class ProgramException : ArgumentException
    {
        internal ProgramException(string message) : base(message)
        {
            
        }

        internal ProgramException(string message, Exception e) : base(message, e)
        {
            
        }
    }
}