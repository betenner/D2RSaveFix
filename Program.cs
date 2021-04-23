using System;
using System.IO;

namespace D2RSaveFix
{
    class Program
    {
        private const byte GAME_COMPLETED_ON_NORMAL = 0x08;

        private const int CHARACTER_PROGRESSION_OFFSET = 0x25;
        private const int CHECKSUM_OFFSET = 0x0C;

        private const int QUESTS_SECTION_OFFSET = 0x014F;

        private const byte WAYPOINTS_A3WP1_ENABLED = 0x04;
        private const int WAYPOINTS_SECTION_OFFSET = 0x0279;
        private const int WAYPOINTS_DATA_OFFSET = 0x08;
        private const int WAYPOINTS_DIFFICULTY_OFFSET = 0x18;

        private const int HEADER_LENGTH = 765;

        private enum Difficulty
        {
            Normal,
            Nightmare,
            Hell
        }

        private enum Act
        {
            TheSightlessEye,
            SecretOfTheVizjerei,
            TheInfernalGate,
            TheHarrowing,
            LordOfDestruction
        };

        private enum Quest
        {
            FirstQuest,
            SecondQuest,
            ThirdQuest,
            FourthQuest,
            FifthQuest,
            SixthQuest
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
                Console.WriteLine("非法D2R存档文件！ 按任意键关闭");
                Console.ReadKey();
                return;
            }

            // 备份
            MakeBackup(fileData, file);

            // 解锁
            UnlockGame(fileData);

            // Checksum
            UpdateChecksum(fileData, CHECKSUM_OFFSET);

            // 写入
            fs.Seek(0, SeekOrigin.Begin);
            fs.Write(fileData, 0, fileData.Length);
            fs.Flush();
            Console.WriteLine("操作成功! 按任意键关闭");
            Console.ReadKey();
        }

        private static void ShowUsage()
        {
            Console.WriteLine("将D2R存档文件(*.d2s)拖到此程序上以实现：\n1. 标记该存档为已经完成普通难度（可进行单机游戏）\n2. 完成所有难度A2最后一个任务（可进入A3）\n3. 解锁所有难度A3第一个路点\n\n按任意键关闭");
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

        private static void EnableA3WP1(byte[] rawSaveFile)
        {
            for (int difficulty = 0; difficulty < 3; difficulty++)
            {
                int firstWpOffset = WAYPOINTS_SECTION_OFFSET +
                    WAYPOINTS_DATA_OFFSET + difficulty * WAYPOINTS_DIFFICULTY_OFFSET;
                rawSaveFile[firstWpOffset + 4] |= WAYPOINTS_A3WP1_ENABLED;
            }
        }

        private static void AllowTravelToNextAct(Difficulty difficulty, Act act, byte[] rawSaveFile)
        {
            if (act != Act.TheHarrowing)
            {
                ChangeQuest(difficulty, act, Quest.SixthQuest, true, rawSaveFile);
            }
            else
            {
                ChangeQuest(difficulty, act, Quest.SecondQuest, true, rawSaveFile);
            }
        }

        private static void CompleteA2(Difficulty difficulty, byte[] rawSaveFile)
        {
            AllowTravelToNextAct(difficulty, Act.SecretOfTheVizjerei, rawSaveFile);
        }
        
        private static void CompleteAllA2(byte[] rawSaveFile)
        {
            CompleteA2(Difficulty.Normal, rawSaveFile);
            CompleteA2(Difficulty.Nightmare, rawSaveFile);
            CompleteA2(Difficulty.Hell, rawSaveFile);
        }

        private static void UnlockGame(byte[] rawSaveFile)
        {
            // 完成普通难度
            rawSaveFile[CHARACTER_PROGRESSION_OFFSET] |= GAME_COMPLETED_ON_NORMAL;

            // 完成所有A2
            //CompleteAllA2(rawSaveFile);

            // 解锁所有A3WP1
            EnableA3WP1(rawSaveFile);
        }

        private static int GetQuestOffset(Difficulty difficulty, Act act, Quest quest)
        {
            int offset = -1;

            if (act != Act.TheHarrowing || quest < Quest.FourthQuest)
            {
                offset = 12;                    // 10 bytes for the quest header, 2 bytes for the act introduction

                offset += (int)difficulty * 96; // choose to the right difficulty
                offset += (int)act * 16;        // choose to the right act
                offset += (int)quest * 2;       // choose the right quest

                if (act == Act.LordOfDestruction)
                {
                    offset += 4;                // there are additional bytes in act 4
                }
            }

            return offset;
        }

        private static void ChangeQuest(Difficulty difficulty, Act act, Quest quest, bool complete, byte[] rawSaveFile)
        {
            int offset = QUESTS_SECTION_OFFSET + GetQuestOffset(difficulty, act, quest);

            if (offset == -1)
            {
                return;
            }

            if (complete)
            {
                rawSaveFile[offset] = 0x01;     // Quest complete
                rawSaveFile[offset + 1] = 0x10; // Quest log animation viewed

                if (act == Act.LordOfDestruction && quest == Quest.ThirdQuest)
                {
                    // Scroll of resist
                    rawSaveFile[offset] += 0xC0;
                }
            }
            else
            {
                rawSaveFile[offset] = 0;
                rawSaveFile[offset + 1] = 0;
            }

            // Allow travel to the next act.
            // For Act4, the diablo quest is quest2
            if (complete && (quest == Quest.SixthQuest || (act == Act.TheHarrowing && quest == Quest.SecondQuest)))
            {
                if (act != Act.TheHarrowing)
                {
                    rawSaveFile[offset + 2] = 1;
                }
                else
                {
                    rawSaveFile[offset + 4] = 1;
                }
            }
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
