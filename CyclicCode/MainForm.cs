using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace CyclicCode
{
    public partial class MainForm : Form
    {
        private readonly Random _rnd = new Random();
        // stores last clean encoded bits (before channel errors)
        private string _lastCleanCodeword = string.Empty;

        public MainForm()
        {
            InitializeComponent();
            this.Load += OnFormLoad;
        }

        private void OnFormLoad(object sender, EventArgs e)
        {
            btnCalcTheory_Click(sender, e);
        }

        // ===================================================================
        // Theory tab
        // ===================================================================
        private void btnCalcTheory_Click(object sender, EventArgs e)
        {
            var sb = new StringBuilder();

            WriteSection(sb, "ПАРАМЕТРЫ ЦИКЛИЧЕСКОГО КОДА BCH");
            sb.AppendLine($"  Вариант 10");
            sb.AppendLine($"  n  = {CyclicCodeLogic.N}  — общая длина кодового слова");
            sb.AppendLine($"  k  = {CyclicCodeLogic.K}  — число информационных разрядов");
            sb.AppendLine($"  r  = {CyclicCodeLogic.R} — число проверочных разрядов (n − k)");
            sb.AppendLine();

            WriteSection(sb, "ПОРОЖДАЮЩИЙ ПОЛИНОМ g(x)");
            sb.AppendLine($"  {CyclicCodeLogic.GeneratorPolyString()}");
            sb.AppendLine($"  Двоичное представление: {CyclicCodeLogic.BitsToStr(CyclicCodeLogic.G)}");
            sb.AppendLine($"  Степень полинома: {CyclicCodeLogic.Degree(CyclicCodeLogic.G)} (должна равняться r = {CyclicCodeLogic.R})");
            sb.AppendLine();

            WriteSection(sb, "ПРОВЕРКА ДЕЛИМОСТИ: g(x) | (x^n + 1)");
            bool ok = CyclicCodeLogic.CheckGeneratorDivisibility(CyclicCodeLogic.G, CyclicCodeLogic.N);
            sb.AppendLine($"  x^{CyclicCodeLogic.N} + 1 делится на g(x): {(ok ? "ДА ✓" : "НЕТ ✗")}");
            sb.AppendLine(ok
                ? "  g(x) является корректным порождающим полиномом для данного кода."
                : "  ВНИМАНИЕ: g(x) не является делителем (x^n+1)!");
            sb.AppendLine();

            WriteSection(sb, "МИНИМАЛЬНОЕ КОДОВОЕ РАССТОЯНИЕ И КОРРЕКТИРУЮЩАЯ СПОСОБНОСТЬ");
            sb.AppendLine($"  d_min                   = {CyclicCodeLogic.DMIN}");
            sb.AppendLine($"  Обнаруживаемые ошибки   = d_min − 1 = {CyclicCodeLogic.DMIN - 1}");
            sb.AppendLine($"  Исправляемые ошибки   t = ⌊(d_min − 1)/2⌋ = ⌊{CyclicCodeLogic.DMIN - 1}/2⌋ = {CyclicCodeLogic.T}");
            sb.AppendLine();

            WriteSection(sb, "ГРАНИЦА ХЭММИНГА");
            var (hL, hR, hSat) = CyclicCodeLogic.HammingBound(CyclicCodeLogic.N, CyclicCodeLogic.K, CyclicCodeLogic.T);
            sb.AppendLine($"  Условие: Σ_{{i=0}}^{{t}} C(n,i) ≤ 2^r");
            sb.AppendLine($"  Σ_{{i=0}}^{CyclicCodeLogic.T} C({CyclicCodeLogic.N},i) = {hL:F0}");
            sb.AppendLine($"  2^r = 2^{CyclicCodeLogic.R} = {hR:F0}");
            sb.AppendLine($"  Граница Хэмминга: {(hSat ? "ВЫПОЛНЕНА ✓" : "НЕ ВЫПОЛНЕНА ✗")}");
            sb.AppendLine();

            WriteSection(sb, "ГРАНИЦА ПЛОТКИНА");
            var (pDesc, pSat) = CyclicCodeLogic.PlotkinBound(CyclicCodeLogic.N, CyclicCodeLogic.K, CyclicCodeLogic.DMIN);
            sb.AppendLine($"  {pDesc}");
            sb.AppendLine($"  Граница Плоткина: {(pSat ? "ВЫПОЛНЕНА ✓" : "НЕ ВЫПОЛНЕНА ✗")}");
            sb.AppendLine();

            WriteSection(sb, "ГРАНИЦА ВАРШАМОВА–ГИЛЬБЕРТА");
            var (vL, vR, vSat) = CyclicCodeLogic.VarshamovGilbertBound(CyclicCodeLogic.N, CyclicCodeLogic.K, CyclicCodeLogic.DMIN);
            sb.AppendLine($"  Условие: Σ_{{i=0}}^{{d−2}} C(n−1,i) < 2^(n−k)");
            sb.AppendLine($"  Σ_{{i=0}}^{CyclicCodeLogic.DMIN - 2} C({CyclicCodeLogic.N - 1},i) = {vL:F0}");
            sb.AppendLine($"  2^(n−k) = 2^{CyclicCodeLogic.R} = {vR:F0}");
            sb.AppendLine($"  Граница Варшамова–Гильберта: {(vSat ? "ВЫПОЛНЕНА ✓" : "НЕ ВЫПОЛНЕНА ✗")}");
            sb.AppendLine();

            WriteSection(sb, "ЗАДАЧА 1 — ПРОВЕРКА P(x) ДЛЯ n=15");
            sb.AppendLine("  P(x) = 10100110111₂  → x^10 + x^8 + x^5 + x^4 + x^2 + x + 1");
            int[] px = { 1, 0, 1, 0, 0, 1, 1, 0, 1, 1, 1 };
            bool pxOk = CyclicCodeLogic.CheckGeneratorDivisibility(px, 15);
            sb.AppendLine($"  (x^15 + 1) mod P(x) = 0 ? → {(pxOk ? "ДА, делится ✓" : "НЕТ, не делится ✗")}");
            sb.AppendLine(pxOk
                ? "  P(x) является порождающим полиномом для циклического кода с n=15."
                : "  P(x) не делит (x^15+1) и не является порождающим полиномом для n=15.");
            sb.AppendLine();

            richTheory.Text = sb.ToString();
            SetStatus("Теоретические данные рассчитаны.");
        }

        // ===================================================================
        // Encoder tab
        // ===================================================================
        private void btnEncode_Click(object sender, EventArgs e)
        {
            string raw = txtInputBits.Text.Trim();
            if (!CheckBitString(raw, CyclicCodeLogic.K, "информационных бит")) return;

            int[] info = ToBitArray(raw);
            try
            {
                int[] cw = CyclicCodeLogic.Encode(info);
                string cwStr = CyclicCodeLogic.BitsToStr(cw);
                txtEncodedBits.Text = cwStr;
                _lastCleanCodeword = cwStr;
                SetStatus($"Закодировано: {raw} → {cwStr}");
            }
            catch (Exception ex)
            {
                ShowError("Ошибка кодирования", ex.Message);
            }
        }

        private void btnCopyToDecoder_Click(object sender, EventArgs e)
        {
            string cw = txtEncodedBits.Text.Trim();
            if (cw.Length == 0)
            {
                ShowWarning("Сначала выполните кодирование.");
                return;
            }
            txtReceivedBits.Text = cw;
            txtDecoderInput.Text = cw;
            tabControl.SelectedTab = tabChannel;
            SetStatus("Кодовое слово передано в канал связи.");
        }

        // ===================================================================
        // Channel tab
        // ===================================================================
        private void btnRandomError_Click(object sender, EventArgs e)
        {
            string bits = txtReceivedBits.Text.Trim();
            if (!CheckBitString(bits, CyclicCodeLogic.N, "бит кодовой комбинации")) return;

            int cnt = (int)numErrorCount.Value;
            char[] arr = bits.ToCharArray();
            var positions = Enumerable.Range(0, CyclicCodeLogic.N)
                                      .OrderBy(_ => _rnd.Next())
                                      .Take(cnt)
                                      .OrderBy(p => p)
                                      .ToList();
            foreach (int p in positions) arr[p] = arr[p] == '0' ? '1' : '0';

            string result = new string(arr);
            txtReceivedBits.Text = result;
            txtDecoderInput.Text = result;
            SetStatus($"Внесено {cnt} ошибок в позициях (1-based): {string.Join(", ", positions.Select(p => p + 1))}");
        }

        private void btnClearErrors_Click(object sender, EventArgs e)
        {
            if (_lastCleanCodeword.Length == 0)
            {
                ShowWarning("Нет закодированного слова для восстановления.");
                return;
            }
            txtReceivedBits.Text = _lastCleanCodeword;
            txtDecoderInput.Text = _lastCleanCodeword;
            SetStatus("Слово восстановлено в исходное (без ошибок).");
        }

        // ===================================================================
        // Decoder tab
        // ===================================================================
        private void btnDecode_Click(object sender, EventArgs e)
        {
            string bits = txtDecoderInput.Text.Trim();
            if (!CheckBitString(bits, CyclicCodeLogic.N, "бит принятой кодовой комбинации")) return;

            int[] received = ToBitArray(bits);
            CyclicCodeLogic.DecodeResult res;
            try
            {
                res = CyclicCodeLogic.Decode(received);
            }
            catch (Exception ex)
            {
                ShowError("Ошибка декодирования", ex.Message);
                return;
            }

            // Populate log with color-coded lines
            richDecoderLog.Clear();
            foreach (string line in res.Log) AddColoredLogLine(line);

            // Populate result panel
            if (res.NoError)
            {
                SetResultStatus("Ошибок не обнаружено ✅", Color.DarkGreen);
                lblResultCorrectedVal.Text = CyclicCodeLogic.BitsToStr(res.CorrectedWord);
                lblResultInfoVal.Text = CyclicCodeLogic.BitsToStr(res.InfoBits);
                lblResultErrPosVal.Text = "—";
            }
            else if (res.Uncorrectable)
            {
                SetResultStatus("Ошибка неисправима (q > t) ❌", Color.DarkRed);
                lblResultCorrectedVal.Text = "Невозможно восстановить";
                lblResultInfoVal.Text = "—";
                lblResultErrPosVal.Text = "—";
            }
            else
            {
                SetResultStatus($"Исправлено {res.ErrorPositions.Length} ошибок ⚠️", Color.DarkOrange);
                lblResultCorrectedVal.Text = CyclicCodeLogic.BitsToStr(res.CorrectedWord);
                lblResultInfoVal.Text = CyclicCodeLogic.BitsToStr(res.InfoBits);
                lblResultErrPosVal.Text = res.ErrorPositions.Length > 0
                    ? string.Join(", ", res.ErrorPositions)
                    : "только проверочные биты";
            }

            tabControl.SelectedTab = tabDecoder;
            SetStatus($"Декодирование завершено: {lblResultStatus.Text}");
        }

        private void btnClearDecoder_Click(object sender, EventArgs e)
        {
            txtDecoderInput.Clear();
            richDecoderLog.Clear();
            SetResultStatus("—", Color.Black);
            lblResultCorrectedVal.Text = "";
            lblResultInfoVal.Text = "";
            lblResultErrPosVal.Text = "";
            SetStatus("Декодер очищен.");
        }

        // ===================================================================
        // Tests tab
        // ===================================================================
        private void btnRunTests_Click(object sender, EventArgs e)
        {
            var sb = new StringBuilder();
            int passed = 0, failed = 0;

            WriteSection(sb, "ТЕСТИРОВАНИЕ КОДЕРА И ДЕКОДЕРА (Алгоритм 3)");
            sb.AppendLine($"  n={CyclicCodeLogic.N}, k={CyclicCodeLogic.K}, r={CyclicCodeLogic.R}, dmin={CyclicCodeLogic.DMIN}, t={CyclicCodeLogic.T}");
            sb.AppendLine();

            var tests = new List<(string Desc, string Info, int[] ErrBits, bool CanFix)>
            {
                ("Тест  1: без ошибок",                "00000", Array.Empty<int>(),                 true),
                ("Тест  2: без ошибок",                "10101", Array.Empty<int>(),                 true),
                ("Тест  3: без ошибок",                "11111", Array.Empty<int>(),                 true),
                ("Тест  4: 1 ошибка  (позиция 1)",     "10110", new[]{0},                           true),
                ("Тест  5: 1 ошибка  (позиция 16)",    "01010", new[]{15},                          true),
                ("Тест  6: 1 ошибка  (позиция 31)",    "11001", new[]{30},                          true),
                ("Тест  7: 2 ошибки",                  "10001", new[]{5, 20},                       true),
                ("Тест  8: 3 ошибки",                  "01110", new[]{1, 10, 25},                   true),
                ("Тест  9: 5 ошибок",                  "11010", new[]{0, 5, 10, 15, 20},            true),
                ("Тест 10: 7 ошибок (макс. t)",        "10100", new[]{0, 4, 8, 12, 16, 20, 24},     true),
                ("Тест 11: 8 ошибок (> t, ожид. fail)","11100", new[]{0, 4, 8, 12, 16, 20, 24, 28}, false),
            };

            foreach (var (desc, infoStr, errBits, canFix) in tests)
            {
                sb.AppendLine(new string('─', 72));
                sb.AppendLine($"  {desc}");
                sb.AppendLine($"  Инф. биты:   {infoStr}");

                int[] info = ToBitArray(infoStr);
                int[] cw;
                try { cw = CyclicCodeLogic.Encode(info); }
                catch (Exception ex) { sb.AppendLine($"  [ENCODE ERROR]: {ex.Message}"); failed++; continue; }

                sb.AppendLine($"  Кодовое слово: {CyclicCodeLogic.BitsToStr(cw)}");

                int[] rcv = (int[])cw.Clone();
                foreach (int p in errBits) rcv[p] ^= 1;
                if (errBits.Length > 0)
                    sb.AppendLine($"  Принято ({errBits.Length} ош.): {CyclicCodeLogic.BitsToStr(rcv)}  [биты: {string.Join(",", errBits.Select(p => p + 1))}]");
                else
                    sb.AppendLine("  Ошибок нет.");

                CyclicCodeLogic.DecodeResult res;
                try { res = CyclicCodeLogic.Decode(rcv); }
                catch (Exception ex) { sb.AppendLine($"  [DECODE ERROR]: {ex.Message}"); failed++; continue; }

                sb.AppendLine($"  Синдром S0: {CyclicCodeLogic.BitsToStr(res.Syndrome)}  (вес={CyclicCodeLogic.HammingWeight(res.Syndrome)})");

                bool testOk;
                if (canFix)
                {
                    bool infoOk = CyclicCodeLogic.BitsToStr(res.InfoBits) == infoStr;
                    bool synZero = !res.Uncorrectable && CyclicCodeLogic.HammingWeight(
                        CyclicCodeLogic.ModPoly(res.CorrectedWord, CyclicCodeLogic.G)) == 0;
                    testOk = infoOk && synZero;
                    if (!res.NoError)
                    {
                        sb.AppendLine($"  Исправлено:  {CyclicCodeLogic.BitsToStr(res.CorrectedWord)}");
                        if (res.ErrorPositions.Length > 0)
                            sb.AppendLine($"  Позиции:     {string.Join(", ", res.ErrorPositions)} (1-based)");
                    }
                    sb.AppendLine($"  Извлечённые биты: {CyclicCodeLogic.BitsToStr(res.InfoBits)}");
                }
                else
                {
                    testOk = res.Uncorrectable;
                }

                sb.AppendLine($"  Итог: {(testOk ? "ПРОЙДЕН ✅" : "ПРОВАЛЕН ❌")}");
                if (testOk) passed++; else failed++;
            }

            sb.AppendLine();
            sb.AppendLine(new string('═', 72));
            sb.AppendLine($"  ИТОГО: Пройдено {passed}/{passed + failed}   |   Провалено: {failed}/{passed + failed}");

            richTests.Text = sb.ToString();
            SetStatus($"Тестирование: {passed} пройдено, {failed} провалено.");
        }

        // ===================================================================
        // Private helpers
        // ===================================================================
        private bool CheckBitString(string s, int expectedLen, string fieldName)
        {
            if (s.Length != expectedLen)
            {
                ShowWarning($"Поле «{fieldName}» должно содержать ровно {expectedLen} символов (0/1).\nТекущая длина: {s.Length}");
                return false;
            }
            if (s.Any(c => c != '0' && c != '1'))
            {
                ShowWarning($"Поле «{fieldName}» должно содержать только символы 0 и 1.");
                return false;
            }
            return true;
        }

        private static int[] ToBitArray(string s) => s.Select(c => c == '1' ? 1 : 0).ToArray();

        private void SetStatus(string msg) => toolStripStatus.Text = msg;

        private void SetResultStatus(string text, Color color)
        {
            lblResultStatus.Text = text;
            lblResultStatus.ForeColor = color;
        }

        private static void ShowWarning(string msg) =>
            MessageBox.Show(msg, "Предупреждение", MessageBoxButtons.OK, MessageBoxIcon.Warning);

        private static void ShowError(string title, string msg) =>
            MessageBox.Show(msg, title, MessageBoxButtons.OK, MessageBoxIcon.Error);

        private void AddColoredLogLine(string line)
        {
            int start = richDecoderLog.TextLength;
            richDecoderLog.AppendText(line + "\n");
            int end = richDecoderLog.TextLength;
            richDecoderLog.Select(start, end - start);

            Color color;
            FontStyle style = FontStyle.Regular;
            if (line.StartsWith("==="))
            { color = Color.DarkBlue; style = FontStyle.Bold; }
            else if (line.Contains("Syndrome = 0") || line.Contains("No errors") || line.Contains("Ошибок нет"))
            { color = Color.DarkGreen; }
            else if (line.Contains("неисправимой") || line.Contains("невозможности") || line.Contains("uncorrectable"))
            { color = Color.DarkRed; style = FontStyle.Bold; }
            else if (line.StartsWith("→") || line.Contains("localized") || line.Contains("Corr"))
            { color = Color.DarkOrange; }
            else if (line.StartsWith("Shift"))
            { color = Color.DimGray; }
            else
            { color = Color.Black; }

            richDecoderLog.SelectionColor = color;
            richDecoderLog.SelectionFont = new Font("Consolas", 9.5f, style);
            richDecoderLog.SelectionLength = 0;
            richDecoderLog.ScrollToCaret();
        }

        private static void WriteSection(StringBuilder sb, string title)
        {
            string bar = new string('═', title.Length + 4);
            sb.AppendLine();
            sb.AppendLine(bar);
            sb.AppendLine($"  {title}");
            sb.AppendLine(bar);
        }
    }
}
