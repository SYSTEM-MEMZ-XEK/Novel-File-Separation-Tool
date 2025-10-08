using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace 小说文件分离器
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            // 设置默认的章节匹配模式
            textBox3.Text = @"第[零一二三四五六七八九十百千\d]+\s*[章回节]|^[上下卷]\s*第?[零一二三四五六七八九十百千\d]+[章回节]?|^[序前言后记尾声]$";

            // 添加正则表达式帮助提示
            toolTip1.SetToolTip(textBox3, "点击右侧的\"正则表达式帮助\"按钮查看使用说明");
            toolTip1.SetToolTip(btnRegexHelp, "点击查看正则表达式使用教程");
        }

        // 选择输入文件
        private void button1_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "文本文件|*.txt|所有文件|*.*";
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    textBox1.Text = openFileDialog.FileName;
                }
            }
        }

        // 选择输出目录
        private void button2_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog folderDialog = new FolderBrowserDialog())
            {
                if (folderDialog.ShowDialog() == DialogResult.OK)
                {
                    textBox2.Text = folderDialog.SelectedPath;
                }
            }
        }

        // 开始分离
        private void button3_Click(object sender, EventArgs e)
        {
            try
            {
                // 验证输入
                if (string.IsNullOrEmpty(textBox1.Text) || !File.Exists(textBox1.Text))
                {
                    MessageBox.Show("请选择有效的输入文件！", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (string.IsNullOrEmpty(textBox2.Text) || !Directory.Exists(textBox2.Text))
                {
                    MessageBox.Show("请选择有效的输出目录！", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // 根据选择的模式进行分离
                if (radioButton1.Checked)
                {
                    SplitByFileSize();
                }
                else if (radioButton2.Checked)
                {
                    SplitByFileCount();
                }
                else if (radioButton3.Checked)
                {
                    SplitByChapter();
                }

                MessageBox.Show("小说分离完成！", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"分离过程中出现错误：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SplitByFileSize()
        {
            long maxSize = (long)numericUpDown1.Value;
            string unit = comboBox1.Text;

            // 转换为字节
            if (unit == "KB") maxSize *= 1024;
            else if (unit == "MB") maxSize *= 1024 * 1024;

            string inputFile = textBox1.Text;
            string outputDir = textBox2.Text;
            string baseName = Path.GetFileNameWithoutExtension(inputFile);

            // 自动检测文件编码
            Encoding encoding = GetFileEncoding(inputFile);

            using (StreamReader reader = new StreamReader(inputFile, encoding))
            {
                int fileCount = 1;
                while (!reader.EndOfStream)
                {
                    string outputFile = Path.Combine(outputDir, $"{baseName}_{fileCount:D4}.txt");
                    // 使用UTF-8编码写入，确保中文正确显示
                    using (StreamWriter writer = new StreamWriter(outputFile, false, Encoding.UTF8))
                    {
                        long currentSize = 0;
                        while (currentSize < maxSize && !reader.EndOfStream)
                        {
                            string line = reader.ReadLine();
                            writer.WriteLine(line);
                            currentSize += Encoding.UTF8.GetByteCount(line + Environment.NewLine);
                        }
                    }
                    fileCount++;
                }
            }
        }

        private void SplitByFileCount()
        {
            int fileCount = (int)numericUpDown2.Value;
            string inputFile = textBox1.Text;
            string outputDir = textBox2.Text;
            string baseName = Path.GetFileNameWithoutExtension(inputFile);

            // 自动检测文件编码
            Encoding encoding = GetFileEncoding(inputFile);

            // 使用检测到的编码读取文件
            string[] allLines = File.ReadAllLines(inputFile, encoding);

            int totalLines = allLines.Length;
            int linesPerFile = (int)Math.Ceiling((double)totalLines / fileCount);

            for (int i = 0; i < fileCount; i++)
            {
                string outputFile = Path.Combine(outputDir, $"{baseName}_{i + 1:D4}.txt");
                int startLine = i * linesPerFile;
                int endLine = Math.Min(startLine + linesPerFile, totalLines);

                // 使用UTF-8编码写入
                using (StreamWriter writer = new StreamWriter(outputFile, false, Encoding.UTF8))
                {
                    for (int j = startLine; j < endLine; j++)
                    {
                        writer.WriteLine(allLines[j]);
                    }
                }
            }
        }

        private void SplitByChapter()
        {
            string chapterPattern = textBox3.Text;
            if (string.IsNullOrEmpty(chapterPattern))
            {
                MessageBox.Show("请输入章节匹配模式！", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string inputFile = textBox1.Text;
            string outputDir = textBox2.Text;
            string baseName = Path.GetFileNameWithoutExtension(inputFile);

            // 自动检测文件编码
            Encoding encoding = GetFileEncoding(inputFile);

            // 使用检测到的编码读取文件
            string[] allLines = File.ReadAllLines(inputFile, encoding);

            List<string> currentChapter = new List<string>();
            int chapterCount = 0;

            for (int i = 0; i < allLines.Length; i++)
            {
                string line = allLines[i];

                // 检查是否是章节标题
                bool isChapter = Regex.IsMatch(line.Trim(), chapterPattern);

                if (isChapter)
                {
                    // 如果是第一个章节，检查前面是否有内容（前言、简介等）
                    if (chapterCount == 0 && currentChapter.Count > 0)
                    {
                        SaveChapter(currentChapter, outputDir, baseName, 0, "前言简介");
                        chapterCount++;
                        currentChapter.Clear();
                    }
                    else if (currentChapter.Count > 0)
                    {
                        // 保存前一章
                        string chapterTitle = ExtractChapterTitle(currentChapter);
                        SaveChapter(currentChapter, outputDir, baseName, chapterCount, chapterTitle);
                        chapterCount++;
                        currentChapter.Clear();
                    }
                }

                currentChapter.Add(line);
            }

            // 保存最后一章
            if (currentChapter.Count > 0)
            {
                string chapterTitle = ExtractChapterTitle(currentChapter);
                SaveChapter(currentChapter, outputDir, baseName, chapterCount, chapterTitle);
            }
        }

        // 从章节内容中提取章节标题
        private string ExtractChapterTitle(List<string> chapterLines)
        {
            if (chapterLines.Count == 0) return "未知章节";

            // 查找可能的章节标题行（通常是前几行）
            for (int i = 0; i < Math.Min(5, chapterLines.Count); i++)
            {
                string line = chapterLines[i].Trim();
                if (!string.IsNullOrEmpty(line) && line.Length < 100) // 假设章节标题不会太长
                {
                    // 检查是否包含常见的章节标识词
                    if (Regex.IsMatch(line, @"第[零一二三四五六七八九十百千\d]+\s*[章回节]") ||
                        Regex.IsMatch(line, @"^[上下卷]\s*第?[零一二三四五六七八九十百千\d]+[章回节]?") ||
                        Regex.IsMatch(line, @"^[序前言后记尾声]$"))
                    {
                        return line;
                    }
                }
            }

            // 如果没有找到明显的章节标题，使用第一行非空行
            foreach (string line in chapterLines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    return line.Length > 50 ? line.Substring(0, 50) + "..." : line;
                }
            }

            return "未知章节";
        }

        private void SaveChapter(List<string> chapterLines, string outputDir, string baseName, int chapterNumber, string chapterTitle)
        {
            if (chapterLines.Count == 0) return;

            string safeFileName = MakeValidFileName(chapterTitle);

            // 限制文件名长度，避免过长
            if (safeFileName.Length > 50)
            {
                safeFileName = safeFileName.Substring(0, 50);
            }

            string outputFile = Path.Combine(outputDir, $"{baseName}_{chapterNumber + 1:D4}_{safeFileName}.txt");

            // 使用UTF-8编码写入
            File.WriteAllLines(outputFile, chapterLines, Encoding.UTF8);
        }

        private string MakeValidFileName(string name)
        {
            string invalidChars = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
            string invalidRegStr = string.Format(@"[{0}]", invalidChars);
            return Regex.Replace(name, invalidRegStr, "_");
        }

        // 自动检测文件编码
        private Encoding GetFileEncoding(string filename)
        {
            try
            {
                // 读取文件的字节序标记(BOM)来判断编码
                using (var reader = new StreamReader(filename, Encoding.Default, true))
                {
                    reader.Peek(); // 必须调用Read或Peek来检测编码
                    return reader.CurrentEncoding;
                }
            }
            catch
            {
                // 如果检测失败，默认使用UTF-8
                return Encoding.UTF8;
            }
        }

        // 正则表达式帮助按钮
        private void btnRegexHelp_Click(object sender, EventArgs e)
        {
            string helpText = @"正则表达式使用教程

常用元字符：
.    - 匹配任意单个字符（除了换行符）
\d   - 匹配数字（0-9）
\w   - 匹配字母、数字或下划线
\s   - 匹配空白字符（空格、制表符等）
[ ]  - 匹配括号内的任意一个字符
[^ ] - 匹配不在括号内的任意一个字符
*    - 匹配前面的元素零次或多次
+    - 匹配前面的元素一次或多次
?    - 匹配前面的元素零次或一次
|    - 或运算符，匹配左边或右边
^    - 匹配行的开始
$    - 匹配行的结束

常用字符集：
[零一二三四五六七八九十百千] - 匹配中文数字
[章回节] - 匹配章节标识字
[上下卷] - 匹配卷标识字

常用模式示例：
1. 第X章模式：第[零一二三四五六七八九十百千\d]+\s*章
   - 匹配：第一章、第123章、第一百章等

2. 第X回模式：第[零一二三四五六七八九十百千\d]+\s*回
   - 匹配：第一回、第25回、第一百二十回等

3. 综合模式：第[零一二三四五六七八九十百千\d]+\s*[章回节]
   - 匹配：第一章、第二回、第三节等

4. 卷X模式：^[上下卷]\s*第?[零一二三四五六七八九十百千\d]+[章回节]?
   - 匹配：上卷第一章、下卷第五回、卷三等

5. 特殊章节：^[序前言后记尾声]$
   - 匹配：序、前言、后记、尾声等

提示：
- 在文本框中输入正则表达式模式
- 可以使用右侧的快捷按钮快速选择常用模式
- 复杂的模式可以组合使用，用 | 分隔";

            MessageBox.Show(helpText, "正则表达式使用教程", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}