# Cyclic BCH Code (31, 5, 26) — Windows Forms Application

A C# (.NET 6 / Windows Forms) application for encoding and decoding using a cyclic BCH code with parameters **(n=31, k=5, r=26)** and **Algorithm 3** (cyclic-shift syndrome decoding).

---

## Code Parameters (Variant 10)

| Parameter | Value | Description |
|-----------|-------|-------------|
| n | 31 | Total codeword length |
| k | 5 | Information bits |
| r | 26 | Check bits (n − k) |
| d_min | 15 | Minimum Hamming distance |
| t | 7 | Maximum correctable errors ⌊(d_min−1)/2⌋ |

**Generator polynomial g(x)** (degree 26) divides x^31 + 1 over GF(2).

---

## Features

### Tab 1 — Theory (Теория)
- Displays all code parameters and the generator polynomial.
- Verifies that g(x) divides x^31 + 1.
- Calculates and checks:
  - **Hamming bound** — Σ C(n,i) ≤ 2^r
  - **Plotkin bound** — 2^k ≤ 2·d_min (when 2·d_min > n)
  - **Varshamov–Gilbert bound** — Σ C(n−1,i) < 2^(n−k)
- Task 1 check: verifies whether P(x) = `10100110111₂` divides x^15 + 1.

### Tab 2 — Encoder (Кодер)
- Input: k=5 information bits (e.g. `10110`).
- Output: n=31 systematic codeword = info bits ∥ check bits.
- Encoding: systematic cyclic method — appends r zeros, divides by g(x), appends remainder.

### Tab 3 — Channel (Канал связи)
- Displays the codeword in an editable field.
- Manual editing: flip any bit to introduce errors.
- Random errors: choose count (1–31) and inject random bit inversions.
- Reset: restore the clean (error-free) codeword.

### Tab 4 — Decoder (Декодер)
- Input: received (possibly corrupted) 31-bit word.
- Runs **Algorithm 3** (cyclic shift + syndrome weight test).
- Full step-by-step log with color coding:
  - Syndrome S0 and its Hamming weight.
  - Each cyclic shift iteration with intermediate syndrome values.
  - Error localization and correction.
  - Verification syndrome (should be 0 after correction).
- Result panel shows:
  - Status (no error / corrected / uncorrectable).
  - Corrected codeword.
  - Extracted information bits (k=5).
  - Error positions (1-based from MSB).

### Tab 5 — Tests (Тестирование)
Runs 11 automated test cases:
- 3 tests: no errors (various info patterns).
- 7 tests: 1–7 correctable errors.
- 1 test: 8 errors (exceeds t=7, expected uncorrectable).

---

## Algorithm 3 — Cyclic Shift Decoding

1. Compute initial syndrome **S0 = F'(x) mod g(x)**.
2. If S0 = 0 → no error detected.
3. While weight(Si) > t:
   - Cyclically shift the received word left by 1 bit.
   - Recompute syndrome Si.
   - Increment shift counter i.
   - If i = n and no syndrome with weight ≤ t found → uncorrectable error.
4. When weight(Si) ≤ t:
   - XOR the shifted word with syndrome (aligned to check bit positions).
   - Cyclically shift right by i positions to restore original alignment.
5. Extract k=5 MSBs as the decoded information.

---

## Project Structure

```
CyclicCode/
├── CyclicCode.csproj       # .NET 6 Windows Forms project file
├── Program.cs              # Application entry point
├── MainForm.cs             # UI event handlers and display logic
├── MainForm.Designer.cs    # Windows Forms designer-generated layout
├── CyclicCodeLogic.cs      # Core: encoding, decoding, polynomial arithmetic, bounds
└── README.md               # This file
```

---

## Building & Running

**Requirements:** .NET 6 SDK or later, Windows (Windows Forms).

```bash
cd CyclicCode
dotnet build
dotnet run
```

Or open `CyclicCode.csproj` in **Visual Studio 2019/2022** and press **F5**.

---

## Test Cases Summary

| Test | Info Bits | Errors | Expected |
|------|-----------|--------|----------|
| 1–3  | various  | 0      | No error |
| 4    | `10110`  | 1 (pos 1) | Corrected |
| 5    | `01010`  | 1 (pos 16) | Corrected |
| 6    | `11001`  | 1 (pos 31) | Corrected |
| 7    | `10001`  | 2      | Corrected |
| 8    | `01110`  | 3      | Corrected |
| 9    | `11010`  | 5      | Corrected |
| 10   | `10100`  | 7 (= t)| Corrected |
| 11   | `11100`  | 8 (> t)| Uncorrectable |

---

> Developed for Laboratory Work, Variant 10. BCH cyclic code (31, 5, 26), Algorithm 3.
