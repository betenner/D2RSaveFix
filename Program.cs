using System;
using System.IO;

namespace D2RSaveFix
{
    class Program
    {
        private const int HEADER_LENGTH = 765;
        private const byte UNLOCK_DIFFICULTIES_BYTE = 8;
        private const int UNLOCK_DIFFICULTIES_POS = 37;
        private const int QUEST_DATA_POS = 345;
        private static readonly int[] KURAST_DOCK_WP_POS = { 645, 669, 693 };
        private const byte UNLOCK_KURAST_DOCK_WP_BYTE = 4;

        private static readonly byte[] QUEST_DATA = new byte[]
        {
            1,0,12,0,0,0,0,0,0,0,0,0,0,0,1,0,1,0,29,144,121,28,253,159,253,159,253,159,229,31,1,
            0,1,0,0,0,1,16,0,0,0,0,0,0,1,26,1,0,1,0,12,0,1,18,0,0,1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
            0,0,0,0,0,0,0,0,0,193,20,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1,0,253,159,253,159,253,159,253,
            159,253,159,253,159,1,0,1,0,29,144,121,28,253,159,253,159,253,159,229,31,1,0,1,0,0,0,
            1,0,0,0,0,0,0,0,1,10,1,0,1,0,4,0,1,2,0,0,1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,
            0,0,193,20,1,128,0,0,0,0,0,0,0,0,0,0,0,0,1,0,253,159,253,159,253,159,253,159,253,159,
            253,159,1,0,1,0,29,144,121,28,253,159,253,159,253,159,229,31,1,0,1,0,0,0,1,0,0,0,1,0,
            0,0,1,2,1,0,1,0,12,0,1,19,1,16,1,0,0,0,0,0,0,0,0,0,0,0,0,0,12,0,0,0,0,0,0,0,0,0,0,0,
            1,128,0,0,0,0,0,0,0,0,0,0,0,0
        };

        static void Main(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                ShowUsage();
                return;
            }

            string file = args[0];
            if (!File.Exists(file))
            {
                ShowUsage();
                return;
            }

            // 读取
            byte[] fileData;
            using FileStream fs = new FileStream(file, FileMode.Open);
            fileData = new byte[fs.Length];
            fs.Read(fileData, 0, (int)fs.Length);

            // 版本检查
            if (!VersionCheck(fileData))
            {
                Console.WriteLine("非法D2R存档文件！\n按任意键关闭");
                Console.ReadKey();
                return;
            }

            // 备份
            MakeBackup(fileData, file);

            // 解锁难度
            fileData[UNLOCK_DIFFICULTIES_POS] = (byte)(fileData[UNLOCK_DIFFICULTIES_POS] | UNLOCK_DIFFICULTIES_BYTE);

            // 解锁章节
            for (int i = 0; i < QUEST_DATA.Length; i++)
            {
                if (QUEST_DATA[i] == 0) continue;
                fileData[QUEST_DATA_POS + i] |= QUEST_DATA[i];
            }

            // 解锁Kurast Docks路点
            foreach (var pos in KURAST_DOCK_WP_POS)
            {
                fileData[pos] |= UNLOCK_KURAST_DOCK_WP_BYTE;
            }

            // Checksum
            UpdateChecksum(fileData, 12);

            // 写入
            fs.Seek(0, SeekOrigin.Begin);
            fs.Write(fileData, 0, fileData.Length);
            fs.Flush();
            Console.WriteLine("所有难度与章节解锁成功!\n按任意键关闭");
            Console.ReadKey();
        }

        private static void ShowUsage()
        {
            Console.WriteLine("将D2R存档文件(*.d2s)拖到此程序上以解锁该存档的所有难度与章节并可进行单机游戏\n按任意键关闭");
            Console.ReadKey();
        }

        private static void MakeBackup(byte[] data, string filepath)
        {
            var path = Path.GetDirectoryName(filepath);
            var fn = Path.GetFileName(filepath);
            string key = DateTime.Now.ToString("yyyyMMddHHmmssfff");
            var backupFile = Path.Combine(path, fn + "." + key + ".bak");
            if (File.Exists(backupFile)) File.Delete(backupFile);
            using FileStream fs = new FileStream(backupFile, FileMode.Create);
            fs.Write(data, 0, data.Length);
            fs.Flush();
        }

        private static bool VersionCheck(byte[] data)
        {
            if (data == null || data.Length < HEADER_LENGTH) return false;
            if (data[0] != 0x55
                || data[1] != 0xaa
                || data[2] != 0x55
                || data[3] != 0xaa) return false;
            if (data[4] < 0x61) return false;
            return true;
        }

        private static void UpdateChecksum(byte[] fileData, int checkSumOffset)
        {
            if (fileData == null || fileData.Length < checkSumOffset + 4) return;

            // Clear out the old checksum
            Array.Clear(fileData, checkSumOffset, 4);

            int[] checksum = new int[4];
            bool carry = false;

            for (int i = 0; i < fileData.Length; ++i)
            {
                int temp = fileData[i] + (carry ? 1 : 0);

                checksum[0] = checksum[0] * 2 + temp;
                checksum[1] *= 2;

                if (checksum[0] > 255)
                {
                    checksum[1] += (checksum[0] - checksum[0] % 256) / 256;
                    checksum[0] %= 256;
                }

                checksum[2] *= 2;

                if (checksum[1] > 255)
                {
                    checksum[2] += (checksum[1] - checksum[1] % 256) / 256;
                    checksum[1] %= 256;
                }

                checksum[3] *= 2;

                if (checksum[2] > 255)
                {
                    checksum[3] += (checksum[2] - checksum[2] % 256) / 256;
                    checksum[2] %= 256;
                }

                if (checksum[3] > 255)
                {
                    checksum[3] %= 256;
                }

                carry = (checksum[3] & 128) != 0;
            }

            for (int i = checkSumOffset; i < checkSumOffset + 4; ++i)
            {
                fileData[i] = (byte)checksum[i - checkSumOffset];
            }
        }
    }
}
