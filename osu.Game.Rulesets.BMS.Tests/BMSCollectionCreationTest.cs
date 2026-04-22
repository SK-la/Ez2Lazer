// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using osu.Game.Collections;
using osu.Game.Rulesets.BMS.Beatmaps;

namespace osu.Game.Rulesets.BMS.Tests
{
    [TestFixture]
    public class BMSCollectionCreationTest
    {
        [Test]
        public void TestCreateCollectionFromSinglePath()
        {
            // 创建临时目录和BMS文件
            string tempDir = Path.Combine(Path.GetTempPath(), $"bms-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            try
            {
                // 创建几个测试用的BMS文件
                var bmsFiles = new List<string>();

                for (int i = 0; i < 3; i++)
                {
                    string bmsFile = Path.Combine(tempDir, $"test{i}.bms");
                    File.WriteAllText(bmsFile, "#TITLE test\n#ARTIST test\n");
                    bmsFiles.Add(bmsFile);
                }

                // 计算MD5哈希值
                var beatmapHashes = new List<string>();

                foreach (string bmsFile in bmsFiles)
                {
                    string md5Hash = BmsPathKeys.ComputeChartPathKey(bmsFile);
                    beatmapHashes.Add(md5Hash);
                }

                // 验证哈希值不为空
                Assert.That(beatmapHashes.Count, Is.EqualTo(3));
                Assert.That(beatmapHashes.All(h => !string.IsNullOrEmpty(h)), Is.True);

                // 模拟创建收藏夹
                string collectionName = Path.GetFileName(tempDir);
                var collection = new BeatmapCollection(collectionName, beatmapHashes);

                // 验证收藏夹创建成功
                Assert.That(collection.Name, Is.EqualTo(collectionName));
                Assert.That(collection.BeatmapMD5Hashes.Count, Is.EqualTo(3));
                Assert.That(collection.BeatmapMD5Hashes, Is.EquivalentTo(beatmapHashes));
            }
            finally
            {
                // 清理临时目录
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [Test]
        public void TestCreateMultipleCollectionsFromMultiplePaths()
        {
            // 创建多个临时目录
            var tempDirs = new List<string>();
            var allCollections = new List<BeatmapCollection>();

            try
            {
                for (int dirIndex = 0; dirIndex < 3; dirIndex++)
                {
                    string tempDir = Path.Combine(Path.GetTempPath(), $"bms-test-dir{dirIndex}-{Guid.NewGuid():N}");
                    Directory.CreateDirectory(tempDir);
                    tempDirs.Add(tempDir);

                    // 在每个目录中创建BMS文件
                    var bmsFiles = new List<string>();

                    for (int i = 0; i < 2; i++)
                    {
                        string bmsFile = Path.Combine(tempDir, $"test{i}.bms");
                        File.WriteAllText(bmsFile, $"#TITLE test{dirIndex}\n#ARTIST test{dirIndex}\n");
                        bmsFiles.Add(bmsFile);
                    }

                    // 计算该目录的哈希值并创建收藏夹
                    var beatmapHashes = new List<string>();

                    foreach (string bmsFile in bmsFiles)
                    {
                        string md5Hash = BmsPathKeys.ComputeChartPathKey(bmsFile);
                        beatmapHashes.Add(md5Hash);
                    }

                    string collectionName = Path.GetFileName(tempDir);
                    var collection = new BeatmapCollection(collectionName, beatmapHashes);
                    allCollections.Add(collection);
                }

                // 验证每个收藏夹都正确创建
                Assert.That(allCollections.Count, Is.EqualTo(3));

                for (int i = 0; i < 3; i++)
                {
                    Assert.That(allCollections[i].BeatmapMD5Hashes.Count, Is.EqualTo(2));
                    Assert.That(allCollections[i].Name, Does.Contain($"dir{i}"));
                }
            }
            finally
            {
                // 清理所有临时目录
                foreach (string tempDir in tempDirs)
                {
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                }
            }
        }

        [Test]
        public void TestComputeChartPathKeyConsistency()
        {
            string testPath = @"E:\BMS\test.bms";

            // 验证相同路径生成相同哈希
            string hash1 = BmsPathKeys.ComputeChartPathKey(testPath);
            string hash2 = BmsPathKeys.ComputeChartPathKey(testPath);

            Assert.That(hash1, Is.EqualTo(hash2));
            Assert.That(hash1, Is.Not.Empty);
        }

        [Test]
        public void TestComputeChartPathKeyDifferentPaths()
        {
            string path1 = @"E:\BMS\test1.bms";
            string path2 = @"E:\BMS\test2.bms";

            // 验证不同路径生成不同哈希
            string hash1 = BmsPathKeys.ComputeChartPathKey(path1);
            string hash2 = BmsPathKeys.ComputeChartPathKey(path2);

            Assert.That(hash1, Is.Not.EqualTo(hash2));
        }

        [Test]
        public void TestCollectionNameFromPath()
        {
            // 测试从路径提取收藏夹名称
            string path1 = @"E:\BMS\MySongs";
            string path2 = @"E:\BMS\MySongs\";
            string path3 = @"C:\Music\BMS Collection";

            string name1 = Path.GetFileName(path1.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            string name2 = Path.GetFileName(path2.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            string name3 = Path.GetFileName(path3.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

            Assert.That(name1, Is.EqualTo("MySongs"));
            Assert.That(name2, Is.EqualTo("MySongs"));
            Assert.That(name3, Is.EqualTo("BMS Collection"));
        }

        [Test]
        public void TestCollectionNameWithDuplicateHandling()
        {
            // 模拟处理重复名称的逻辑
            var usedCollectionNames = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            // 第一个 "BMS Collection"
            string name1 = "BMS Collection";

            if (usedCollectionNames.TryGetValue(name1, out int name1Count))
            {
                name1Count++;
                usedCollectionNames[name1] = name1Count;
                name1 = $"{name1} ({name1Count})";
            }
            else
            {
                usedCollectionNames[name1] = 0;
            }

            Assert.That(name1, Is.EqualTo("BMS Collection"));

            // 第二个 "BMS Collection" -> 应该变成 "BMS Collection (1)"
            string name2 = "BMS Collection";

            if (usedCollectionNames.TryGetValue(name2, out int name2Count))
            {
                name2Count++;
                usedCollectionNames[name2] = name2Count;
                name2 = $"{name2} ({name2Count})";
            }
            else
            {
                usedCollectionNames[name2] = 0;
            }

            Assert.That(name2, Is.EqualTo("BMS Collection (1)"));

            // 第三个 "BMS Collection" -> 应该变成 "BMS Collection (2)"
            string name3 = "BMS Collection";

            if (usedCollectionNames.TryGetValue(name3, out int name3Count))
            {
                name3Count++;
                usedCollectionNames[name3] = name3Count;
                name3 = $"{name3} ({name3Count})";
            }
            else
            {
                usedCollectionNames[name3] = 0;
            }

            Assert.That(name3, Is.EqualTo("BMS Collection (2)"));
        }
    }
}
