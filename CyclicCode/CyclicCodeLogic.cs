using System;
using System.Collections.Generic;
using System.Text;

namespace CyclicCode
{
    /// <summary>
    /// Core logic for cyclic BCH (31, 5, 26) code with Algorithm 3.
    /// Generator polynomial g(x) of degree 26, dmin=15, t=7.
    /// </summary>
    public static class CyclicCodeLogic
    {
        // Code parameters
        public const int N = 31;   // Block length
        public const int K = 5;    // Information bits
        public const int R = 26;   // Check bits (= N - K)

        // Generator polynomial g(x) for BCH(31,5,26), degree 26.
        // This polynomial divides x^31 + 1 over GF(2).
        // BCH(31,5,26): product of minimal polynomials of GF(2^5) elements
        // for all non-trivial cyclotomic cosets except the one that
        // corresponds to k=5 free information bits.
        //
        // Array length = 27 (degree 26 → 27 coefficients).
        // Index 0 = coefficient of x^26, index 26 = coefficient of x^0.
        // Binary: 1 0 1 0 0 1 1 0 1 1 1 0 1 1 0 0 1 0 1 1 1 1 1 0 0 1 1
        // g(x) = x^26 + x^24 + x^21 + x^20 + x^18 + x^17 + x^16
        //      + x^14 + x^13 + x^10 + x^8  + x^7  + x^6  + x^5  + x^4 + x + 1
        public static readonly int[] G = new int[]
        {
            1,0,1,0,0,1,1,0,1,1,1,0,1,1,0,0,1,0,1,1,1,1,1,0,0,1,1
        }; // length = 27 = R+1; index 0 = x^26, index 26 = x^0
        // dmin for BCH(31,5,26)
        public const int DMIN = 15;
        public const int T = 7; // floor((dmin-1)/2)
        // ---------------------------------------------------------------
        // Polynomial arithmetic over GF(2)
        // Convention: index 0 = highest-degree coefficient.
        // ---------------------------------------------------------------
        /// <summary>Returns the degree of a polynomial. Returns -1 for the zero polynomial.</summary>
        public static int Degree(int[] poly)
        {
            for (int i = 0; i < poly.Length; i++)
                if (poly[i] != 0) return poly.Length - 1 - i;
            return -1;
        }
        /// <summary>Trims leading zero coefficients.</summary>
        public static int[] Trim(int[] poly)
        {
            int start = 0;
            while (start < poly.Length - 1 && poly[start] == 0) start++;
            int[] result = new int[poly.Length - start];
            Array.Copy(poly, start, result, 0, result.Length);
            return result;
        }
        /// <summary>GF(2) addition (XOR) of two polynomials.</summary>
        public static int[] XorPoly(int[] a, int[] b)
        {
            int lenA = a.Length, lenB = b.Length;
            int maxLen = Math.Max(lenA, lenB);
            int[] result = new int[maxLen];
            for (int i = 0; i < maxLen; i++)
            {
                int ai = i < maxLen - lenA ? 0 : a[i - (maxLen - lenA)];
                int bi = i < maxLen - lenB ? 0 : b[i - (maxLen - lenB)];
                result[i] = ai ^ bi;
            }
            return result;
        }
        /// <summary>
        /// Polynomial remainder: a mod b over GF(2).
        /// Returns array of length deg(b) containing the remainder coefficients.
        /// </summary>
        public static int[] ModPoly(int[] a, int[] b)
        {
            int[] rem = (int[])a.Clone();
            int degB = Degree(b);
            if (degB < 0) throw new DivideByZeroException("Division by zero polynomial.");
            for (int i = 0; i <= rem.Length - b.Length; i++)
            {
                if (rem[i] == 1)
                {
                    for (int j = 0; j < b.Length; j++)
                        rem[i + j] ^= b[j];
                }
            }
            // Last deg(b) positions hold the remainder
            int[] remainder = new int[degB];
            if (degB > 0)
                Array.Copy(rem, rem.Length - degB, remainder, 0, degB);
            return remainder;
        }
        /// <summary>Hamming weight (number of 1 bits).</summary>
        public static int HammingWeight(int[] bits)
        {
            int w = 0;
            foreach (int b in bits) if (b != 0) w++;
            return w;
        }
        // ---------------------------------------------------------------
        // Encoder
        // ---------------------------------------------------------------
        /// <summary>
        /// Systematic cyclic encoding: appends r check bits to k info bits.
        /// F(x) = I(x)*x^r + [ I(x)*x^r mod g(x) ]
        /// </summary>
        public static int[] Encode(int[] info)
        {
            if (info.Length != K)
                throw new ArgumentException($"Information word must be {K} bits.");
            // Multiply by x^R: place info bits at positions [0..K-1], zeros at [K..N-1]
            int[] shifted = new int[N];
            Array.Copy(info, 0, shifted, 0, K);
            // Compute check bits = remainder of division by g(x)
            int[] remainder = ModPoly(shifted, G);
            // Pad remainder to length R (align to right)
            int[] remPadded = new int[R];
            Array.Copy(remainder, 0, remPadded, R - remainder.Length, remainder.Length);
            // Systematic codeword: [info | check]
            int[] codeword = new int[N];
            Array.Copy(info, 0, codeword, 0, K);
            for (int i = 0; i < R; i++)
                codeword[K + i] = remPadded[i];
            return codeword;
        }
        // ---------------------------------------------------------------
        // Decoder — Algorithm 3 (cyclic shift + syndrome weight)
        // ---------------------------------------------------------------
        public class DecodeResult
        {
            public bool NoError      { get; set; }
            public bool Corrected    { get; set; }
            public bool Uncorrectable { get; set; }
            public int[] ReceivedWord  { get; set; } = Array.Empty<int>();
            public int[] CorrectedWord { get; set; } = Array.Empty<int>();
            public int[] InfoBits      { get; set; } = Array.Empty<int>();
            /// <summary>1-based error positions in the original received word.</summary>
            public int[] ErrorPositions { get; set; } = Array.Empty<int>();
            public List<string> Log { get; set; } = new List<string>();
            public int[] Syndrome   { get; set; } = Array.Empty<int>();
        }
        private static int[] CyclicShiftLeft(int[] word)
        {
            int[] result = new int[word.Length];
            Array.Copy(word, 1, result, 0, word.Length - 1);
            result[word.Length - 1] = word[0];
            return result;
        }
        private static int[] CyclicShiftRight(int[] word)
        {
            int[] result = new int[word.Length];
            Array.Copy(word, 0, result, 1, word.Length - 1);
            result[0] = word[word.Length - 1];
            return result;
        }
        /// <summary>
        /// Decodes a received n-bit word using Algorithm 3 (cyclic-shift decoding).
        /// </summary>
        public static DecodeResult Decode(int[] received)
        {
            var res = new DecodeResult();
            res.ReceivedWord = (int[])received.Clone();
            if (received.Length != N)
            {
                res.Log.Add($"[ERROR] Received word must be {N} bits, got {received.Length}.");
                res.Uncorrectable = true;
                return res;
            }
            res.Log.Add("=== DECODER (Algorithm 3) ===");
            res.Log.Add($"Received word:   {BitsToStr(received)}");
            // Step 1 — compute initial syndrome S0
            int[] syndrome = ModPoly(received, G);
            int[] syndPadded = PadLeft(syndrome, R - 1);
            res.Syndrome = syndPadded;
            res.Log.Add($"Initial syndrome S0: {BitsToStr(syndPadded)} (weight={HammingWeight(syndPadded)})");
            // Step 2 — zero syndrome means no detectable error
            if (HammingWeight(syndrome) == 0)
            {
                res.NoError = true;
                res.CorrectedWord = (int[])received.Clone();
                res.InfoBits = ExtractInfo(received);
                res.Log.Add("Syndrome = 0 → No errors detected.");
                res.Log.Add($"Info bits: {BitsToStr(res.InfoBits)}");
                return res;
            }
            // Steps 3–4 — cyclic shift loop
            int[] current = (int[])received.Clone();
            int shifts = 0;
            bool found = false;
            for (int i = 0; i < N; i++)
            {
                syndrome = ModPoly(current, G);
                syndPadded = PadLeft(syndrome, R - 1);
                int w = HammingWeight(syndPadded);
                if (i > 0)
                    res.Log.Add($"Shift {i,2}: word={BitsToStr(current)}, S{i}={BitsToStr(syndPadded)} (weight={w})");
                if (w <= T)
                {
                    res.Log.Add($"→ Weight {w} ≤ t={T}: errors localized at shift {i}.");
                    shifts = i;
                    found = true;
                    break;
                }
                current = CyclicShiftLeft(current);
            }
            if (!found)
            {
                res.Uncorrectable = true;
                res.Log.Add($"No syndrome with weight ≤ t={T} found after {N} shifts.");
                res.Log.Add("Error is uncorrectable (кратность ошибки > t).");
                return res;
            }
            // Step 5 — apply correction
            syndrome = ModPoly(current, G);
            syndPadded = PadLeft(syndrome, R - 1);
            res.Log.Add($"Syndrome at shift {shifts}: {BitsToStr(syndPadded)}");
            // Align syndrome to full n-bit word (syndrome occupies check bit positions)
            int[] syndFull = new int[N];
            Array.Copy(syndPadded, 0, syndFull, N - (R - 1), R - 1);
            res.Log.Add($"Syndrome (n-bit aligned): {BitsToStr(syndFull)}");
            int[] corrShifted = new int[N];
            for (int i = 0; i < N; i++)
                corrShifted[i] = current[i] ^ syndFull[i];
            res.Log.Add($"Corrected (at shift {shifts}): {BitsToStr(corrShifted)}");
            // Shift back right by 'shifts' positions
            int[] corrected = (int[])corrShifted.Clone();
            for (int i = 0; i < shifts; i++)
                corrected = CyclicShiftRight(corrected);
            res.Log.Add($"Corrected word (after {shifts} right shifts): {BitsToStr(corrected)}");
            // Determine changed bit positions
            var errPos = new List<int>();
            for (int i = 0; i < N; i++)
                if (received[i] != corrected[i]) errPos.Add(i + 1); // 1-based
            res.ErrorPositions = errPos.ToArray();
            if (errPos.Count > 0)
                res.Log.Add($"Error positions (1-based from MSB): {string.Join(", ", errPos)}");
            else
                res.Log.Add("No information-bit corrections needed (errors were in check bits).");
            // Verify: syndrome of corrected word should be 0
            int[] verifySynd = ModPoly(corrected, G);
            res.Log.Add($"Verification syndrome: {BitsToStr(PadLeft(verifySynd, R - 1))} (must be 0)");
            res.Corrected = true;
            res.CorrectedWord = corrected;
            res.InfoBits = ExtractInfo(corrected);
            res.Log.Add($"Extracted info bits: {BitsToStr(res.InfoBits)}");
            return res;
        }
        /// <summary>Returns the first k bits of a systematic codeword.</summary>
        public static int[] ExtractInfo(int[] codeword)
        {
            int[] info = new int[K];
            Array.Copy(codeword, 0, info, 0, K);
            return info;
        }
        // ---------------------------------------------------------------
        // Theoretical bounds
        // ---------------------------------------------------------------
        /// <summary>Returns true if poly divides x^n + 1 over GF(2).</summary>
        public static bool CheckGeneratorDivisibility(int[] poly, int n)
        {
            int[] xn1 = new int[n + 1];
            xn1[0] = 1;   // x^n
            xn1[n] = 1;   // +1
            int[] rem = ModPoly(xn1, poly);
            return HammingWeight(rem) == 0;
        }
        /// <summary>Hamming bound: Σ C(n,i) for i=0..t ≤ 2^r</summary>
        public static (double LHS, double RHS, bool Satisfied) HammingBound(int n, int k, int t)
        {
            double lhs = 0;
            double rhs = Math.Pow(2, n - k);
            for (int i = 0; i <= t; i++) lhs += Binomial(n, i);
            return (lhs, rhs, lhs <= rhs);
        }
        /// <summary>Plotkin bound.</summary>
        public static (string Description, bool Satisfied) PlotkinBound(int n, int k, int dmin)
        {
            if (2 * dmin > n)
            {
                double lhs = Math.Pow(2, k);
                double rhs = 2.0 * dmin;
                bool ok = lhs <= rhs;
                return ($"2^k = {lhs:F0} ≤ 2·d_min = {rhs:F0}", ok);
            }
            else
            {
                double rhs = (double)n * Math.Pow(2, n - k - 1) / (Math.Pow(2, n - k) - 1);
                return ($"d_min ≤ {rhs:F4} (Plotkin upper bound)", dmin <= Math.Ceiling(rhs));
            }
        }
        /// <summary>Varshamov–Gilbert bound: Σ C(n-1,i) for i=0..d-2 &lt; 2^(n-k)</summary>
        public static (double LHS, double RHS, bool Satisfied) VarshamovGilbertBound(int n, int k, int dmin)
        {
            double lhs = 0;
            double rhs = Math.Pow(2, n - k);
            for (int i = 0; i <= dmin - 2; i++) lhs += Binomial(n - 1, i);
            return (lhs, rhs, lhs < rhs);
        }
        private static double Binomial(int n, int k)
        {
            if (k < 0 || k > n) return 0;
            if (k == 0 || k == n) return 1;
            double result = 1;
            for (int i = 0; i < k; i++)
            {
                result *= (n - i);
                result /= (i + 1);
            }
            return result;
        }
        // ---------------------------------------------------------------
        // Utility
        // ---------------------------------------------------------------
        public static string BitsToStr(int[] bits)
        {
            var sb = new StringBuilder(bits.Length);
            foreach (int b in bits) sb.Append(b);
            return sb.ToString();
        }
        private static int[] PadLeft(int[] arr, int targetLen)
        {
            if (arr.Length >= targetLen) return arr;
            int[] result = new int[targetLen];
            Array.Copy(arr, 0, result, targetLen - arr.Length, arr.Length);
            return result;
        }
        /// <summary>Returns a human-readable polynomial string for the generator polynomial G.</summary>
        public static string GeneratorPolyString()
        {
            var sb = new StringBuilder("g(x) = ");
            bool first = true;
            for (int i = 0; i < G.Length; i++)
            {
                if (G[i] != 0)
                {
                    int deg = G.Length - 1 - i;
                    if (!first) sb.Append(" + ");
                    if (deg == 0)      sb.Append("1");
                    else if (deg == 1) sb.Append("x");
                    else               sb.Append($"x^{deg}");
                    first = false;
                }
            }
            return sb.ToString();
        }
    }
}
