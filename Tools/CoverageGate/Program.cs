using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;

namespace Gear.CoverageGate
{
    /// <summary>
    /// 测试结果与覆盖率门槛（CI 用，不依赖 Unity/包管理器）。
    ///
    /// <para>
    /// 两个层面：
    /// 1. **用例数棘轮**（始终生效）：从 Unity Test Runner 的 NUnit XML 里统计用例总数，
    ///    低于 baseline 就失败 —— 防止"删测试"或"测试文件没被编译进程序集"悄悄通过 CI。
    /// 2. **行覆盖率门槛**（有覆盖率数据时生效）：解析 Unity Code Coverage 包产出的
    ///    OpenCover 格式 Coverage.xml，低于阈值失败。
    /// </para>
    ///
    /// <para>
    /// 覆盖率数据需要工程装有 <c>com.unity.testtools.codecoverage</c>。
    /// 本工程不在 manifest 里常驻该包（离线打开工程时无法下载会直接卡住导入），
    /// 因此 CI 里通过仓库变量 <c>GEAR_ENABLE_COVERAGE=true</c> 动态注入，默认关闭：
    /// 默认只跑用例数棘轮，覆盖率门槛在注入后自动生效。
    /// </para>
    /// </summary>
    public static class Program
    {
        sealed class Options
        {
            public string ResultsDir = "TestResults";
            public string BaselinePath = "Tools/CoverageGate/baseline.json";
            public int? MinTests;
            public double? MinCoverage;
            public bool RequireCoverage;
        }

        public static int Main(string[] args)
        {
            Options opt;
            try
            {
                opt = Parse(args);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[coverage-gate] 参数错误: " + ex.Message);
                PrintUsage();
                return 2;
            }

            var baseline = LoadBaseline(opt.BaselinePath);
            int minTests = opt.MinTests ?? baseline.MinTests;
            double minCoverage = opt.MinCoverage ?? baseline.MinCoverage;

            if (!Directory.Exists(opt.ResultsDir))
            {
                Console.Error.WriteLine($"[coverage-gate] 找不到测试结果目录: {opt.ResultsDir}");
                return 2;
            }

            var xmlFiles = Directory.GetFiles(opt.ResultsDir, "*.xml", SearchOption.AllDirectories);
            if (xmlFiles.Length == 0)
            {
                Console.Error.WriteLine($"[coverage-gate] {opt.ResultsDir} 下没有任何 .xml 测试结果（测试是否真的跑了？）");
                return 2;
            }

            // ===== 1. 用例统计 =====
            int total = 0, passed = 0, failed = 0, skipped = 0;
            int runs = 0;
            foreach (var file in xmlFiles)
            {
                XDocument doc;
                try
                {
                    doc = XDocument.Load(file);
                }
                catch
                {
                    continue;
                }
                var run = doc.Root;
                if (run == null || run.Name.LocalName != "test-run") continue;

                runs++;
                total += ReadInt(run, "total");
                passed += ReadInt(run, "passed");
                failed += ReadInt(run, "failed");
                skipped += ReadInt(run, "skipped") + ReadInt(run, "inconclusive");
            }

            if (runs == 0)
            {
                Console.Error.WriteLine($"[coverage-gate] {xmlFiles.Length} 个 xml 里没有 NUnit 的 <test-run> 根节点，无法统计用例");
                return 2;
            }

            Console.WriteLine($"[coverage-gate] 测试程序集 {runs} 个，用例 {total}（通过 {passed} / 失败 {failed} / 跳过 {skipped}）");

            int exit = 0;
            if (failed > 0)
            {
                Console.Error.WriteLine($"[coverage-gate] 有 {failed} 个用例失败");
                exit = 1;
            }
            if (total < minTests)
            {
                Console.Error.WriteLine($"[coverage-gate] 用例数 {total} 低于门槛 {minTests}：删测试/测试未编译都会被这里拦住");
                exit = 1;
            }

            // ===== 2. 覆盖率 =====
            var coverageFiles = xmlFiles.Where(f =>
            {
                var name = Path.GetFileName(f);
                return name.Equals("Coverage.xml", StringComparison.OrdinalIgnoreCase) ||
                       name.EndsWith(".coverage.xml", StringComparison.OrdinalIgnoreCase);
            }).ToList();

            double? coverage = coverageFiles.Count == 0 ? null : ReadCoverage(coverageFiles);

            if (coverage == null)
            {
                if (opt.RequireCoverage)
                {
                    Console.Error.WriteLine("[coverage-gate] 要求覆盖率数据，但没有找到 Coverage.xml：" +
                                            "请给工程装上 com.unity.testtools.codecoverage 并用 -enableCodeCoverage 运行");
                    exit = 1;
                }
                else
                {
                    Console.WriteLine("[coverage-gate] 未找到覆盖率数据，仅执行用例数棘轮（启用方法见 Docs/guides/测试与覆盖率.md）");
                }
            }
            else
            {
                Console.WriteLine($"[coverage-gate] 行覆盖率 {coverage.Value.ToString("F1", CultureInfo.InvariantCulture)}%（门槛 {minCoverage.ToString("F1", CultureInfo.InvariantCulture)}%）");
                if (coverage.Value + 1e-9 < minCoverage)
                {
                    Console.Error.WriteLine($"[coverage-gate] 覆盖率低于门槛 {minCoverage}%");
                    exit = 1;
                }
            }

            Console.WriteLine(exit == 0 ? "[coverage-gate] 通过" : "[coverage-gate] 未通过");
            return exit;
        }

        static int ReadInt(XElement el, string attribute)
        {
            var raw = (string)el.Attribute(attribute);
            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;
        }

        /// <summary>
        /// 从 OpenCover 的 &lt;CoverageSession&gt;&lt;Summary&gt; 读取行覆盖率。
        /// Unity Code Coverage 包输出的就是这个格式；没有 Summary 时按 SequencePoint 自行统计。
        /// </summary>
        static double? ReadCoverage(List<string> files)
        {
            long visited = 0, points = 0;
            foreach (var file in files)
            {
                XDocument doc;
                try
                {
                    doc = XDocument.Load(file);
                }
                catch
                {
                    continue;
                }

                var summary = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Summary");
                if (summary != null)
                {
                    if (long.TryParse((string)summary.Attribute("visitedSequencePoints"), out var v) &&
                        long.TryParse((string)summary.Attribute("numSequencePoints"), out var n) && n > 0)
                    {
                        visited += v;
                        points += n;
                        continue;
                    }
                    if (double.TryParse((string)summary.Attribute("sequenceCoverage"),
                            NumberStyles.Float, CultureInfo.InvariantCulture, out var direct))
                    {
                        return direct;
                    }
                }

                foreach (var sp in doc.Descendants().Where(e => e.Name.LocalName == "SequencePoint"))
                {
                    points++;
                    if (long.TryParse((string)sp.Attribute("vc"), out var vc) && vc > 0) visited++;
                }
            }

            if (points == 0) return null;
            return 100.0 * visited / points;
        }

        sealed class Baseline
        {
            public int MinTests { get; set; }
            public double MinCoverage { get; set; }
        }

        static Baseline LoadBaseline(string path)
        {
            var fallback = new Baseline { MinTests = 1, MinCoverage = 0 };
            if (!File.Exists(path))
            {
                Console.WriteLine($"[coverage-gate] 基线文件不存在（{path}），使用默认值");
                return fallback;
            }

            try
            {
                using var stream = File.OpenRead(path);
                var parsed = JsonSerializer.Deserialize<Baseline>(stream, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                });
                return parsed ?? fallback;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[coverage-gate] 基线文件解析失败（{path}）: {ex.Message}");
                return fallback;
            }
        }

        static Options Parse(string[] args)
        {
            var opt = new Options();
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--results":
                        opt.ResultsDir = Next(args, ref i);
                        break;
                    case "--baseline":
                        opt.BaselinePath = Next(args, ref i);
                        break;
                    case "--min-tests":
                        opt.MinTests = int.Parse(Next(args, ref i), CultureInfo.InvariantCulture);
                        break;
                    case "--min-coverage":
                        opt.MinCoverage = double.Parse(Next(args, ref i), CultureInfo.InvariantCulture);
                        break;
                    case "--require-coverage":
                        opt.RequireCoverage = true;
                        break;
                    case "-h":
                    case "--help":
                        PrintUsage();
                        Environment.Exit(0);
                        break;
                    default:
                        throw new ArgumentException("未知参数: " + args[i]);
                }
            }
            return opt;
        }

        static string Next(string[] args, ref int i)
        {
            if (i + 1 >= args.Length) throw new ArgumentException(args[i] + " 缺少取值");
            return args[++i];
        }

        static void PrintUsage()
        {
            Console.WriteLine(@"用法: dotnet run --project Tools/CoverageGate -- [选项]

  --results <目录>        测试结果目录（默认 TestResults，递归查找 .xml）
  --baseline <文件>       门槛基线 json（默认 Tools/CoverageGate/baseline.json）
  --min-tests <n>         覆盖基线里的最少用例数
  --min-coverage <百分>   覆盖基线里的行覆盖率门槛
  --require-coverage      没有覆盖率数据时判失败（默认只告警）
");
        }
    }
}
