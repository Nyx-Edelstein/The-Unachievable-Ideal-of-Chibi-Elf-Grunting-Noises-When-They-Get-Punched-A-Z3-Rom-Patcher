using System;
using System.IO.Enumeration;

namespace z3_oof_patcher
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var romFilename = "";
            var sampleFilename = "";
            var outputFilename = "";

            for (var i = 0; i < args.Length - 1; i++)
            {
                var arg = args[i];
                switch (arg)
                {
                    case "--rom":
                        romFilename = args[i + 1];
                        break;
                    case "--sample":
                        sampleFilename = args[i + 1];
                        break;
                    case "--output":
                        outputFilename = args[i + 1];
                        break;
                }
            }

            try
            {
                //Ease of use feature: patch the first (presumably only) .sfc file in the directory if there are no arguments
                if (romFilename == string.Empty)
                    romFilename = FindRomFile();

                var romFile = GetRomFile(romFilename);
                var sampleFile = GetSampleFile(sampleFilename);

                if (outputFilename == string.Empty)
                    outputFilename = "patched_" + romFilename;

                ApplyPatch(romFile, sampleFile, outputFilename);
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
                        Console.WriteLine($"{e.GetType()}: " + e.Message);
                    Console.WriteLine(e.StackTrace);
                    e = e.InnerException;
                }
            }
        }

        private static string FindRomFile()
        {
            var romFilename = "";

            var files = Directory.GetFiles(".\\");
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
                var fileStream = File.OpenRead($".\\{romFilename}");
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
            if (sampleFilename == string.Empty) return null;

            //Try to open the file
            byte[] bytes;
            try
            {
                var fileStream = File.OpenRead($".\\{sampleFilename}");
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
            if (bytes.Length != 576)
                throw new ProgramException("Sample must be exactly 576 bytes (it could be shorter but needs to be padded)");

            return bytes;
        }

        private static void ApplyPatch(byte[] romFile, byte[] sampleFile, string outputFilename)
        {
            var patches = GetPatches(sampleFile);

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
        private static List<Patch> GetPatches(byte[] customSample)
        {
            var patches = new List<Patch>();

            //Relocate original SPU load routine
            var byteStr1 = "5C888839EA60EAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEAEA";
            patches.Add(new Patch(0x888, byteStr1));

            //Relocate SPU subroutine handling CPU I/0 3:
            var byteStr2 = "5F00BD0000000000000000000000";
            patches.Add(new Patch(0xCFCBE, byteStr2));

            //Pitch shift value:
            //(Original sample is shifted to "B0"; this attempts to correct that so you don't have to fiddle with input samples)
            patches.Add(new Patch(0xD1BF8, "AA"));

            //New SPU load routine:
            var byteStr3 = "08C230A00000A9AABBCD4021D0FBE220A9CC8030B700C8EBA9008015EBB700C8C00080D005A00000E602EBCD4021D0FB1AC2208D4021E220CAD0E1CD4021D0FB6903F0FC48C220B700C8C8AAB700802C8D4221E220E00100A9002A8D4121697F688D4021CD4021D0FB70A99C40219C41219C42219C4321285C8D8800C8C808C03753F0032880C928E220A9608500A9F18501A9398502C220A00000B700C8C8AAB700C8C88D4221E220E00100A9002A8D4121697F688D4021CD4021D0FBB700C8EBA9008015EBB700C8C00080D005A00000E602EBCD4021D0FB1AC2208D4021E220CAD0E1CD4021D0FB6903F0FC48A9008500EAA980EA8501EAA917EA8502C220A90000AAA93753A8A900085CD88839EAEAEAEAEAEAEAEAEA";
            patches.Add(new Patch(0x1C8888, byteStr3));

            //SPU data chunk:
            //Format:
            // - 2 bytes chunk size to copy
            // - 2 bytes location in SPU memory to store
            // - 16 null bytes (buffer, SPU does weird shit if its not there)
            // - 576 bytes brr sound sample
            // - 16 null bytes (again, buffer)
            // - 76 bytes SPU subroutine handling CPU I/O 3
            var byteStr4 = "0003A0BA000000000000000000000000000000009066AF0E73C529C884A4FE1BF71F3A83147CB4C0044DE0E33B03D1A06EB531088FF04B93A03040D4414EA1204294975EB9E52E09C77FACFF144DCE22110DE2A821EDC1133FD050F19C8864E7F8E3309EE0AC50A12042F00C32EFA80F4FF2CC31F42BC5AC3EF5FE4FD12D21DF98D1429E61E41E42BEA41E17519977B9CAE5BCECF3231CD2223CA1A40550BBC03FCCBF32A4254322210F10BA47A4C8FEDFD030D23104A452E154ECEECF1FBD9C52A0773DBE33F308ACF6FCCE33FFDD4310A4FE6732114443CD22A410A8BB01EBDE2206A80901529E70B040FCA825D8264DA14FE4EEAC1033AD53CF0131C0AC2AF72CEE24ED3FD498190316E8E535F8128813E925FC1439E5B09C2DF4212DB0760FBC9C03539834FFE13CB4AC0F33CF0F34FFFD159C0FFAD60C22DDE40C98F53CAF4210FEB37298AA271B22E0E031D5944A8B132DBFFD030C94F32DD2510F121E219C02CE60C41825011C94032ECC27398D015194AAAF51CFECF1670B9404650E01352EF54F9CA074CDED241FDD3284AB51C2DA67262AD38C72AE0E172DE2FDD29421E01CC0010BA15298BD0440DCD46DB5FC9834DEF142CF3E02D38C3D10BD214FA020E18C21DB041101ECE65F8CFCC54030CEF163BD886EB52C42C0FC45EF8CED02140BC054DE2D7823D17EA5EA561AC47477520D992760980F78F3F10AC60E52CDF37C3EB35F0FA07111AF7C51C1202EA26B24BB7421F3D804DE10130C70BA9073ECB175770C744441CD20D1200DC0686DCDE076BDDC771964C8C376ED11F11E2160F14144FF1F24D9F265C0112FC34D790000000000000000000000000000000000000D6826F0166830F02A8EFDD000F408DB08DE08028D00DB005FFE08E8B0C55C3CE8BAC55D3CE8E0C55E3CE8BCC55F3CE8262FD6E8ECC55C3CE8B0C55D3CE82CC55E3CE8B3C55F3CE8302FBE00";
            var chunkPatch = new Patch(0x1CF160, byteStr4);
            if (customSample != null)
            {
                var offset = 20;
                foreach (var b in customSample)
                {
                    chunkPatch.bytes[offset] = b;
                    offset++;
                }
            }
            patches.Add(chunkPatch);

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
                throw new ProgramException("Invalid hex string", e);
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