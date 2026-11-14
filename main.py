"""
Помехоустойчивое кодирование — Циклический код Хэмминга (7,4)
Cyclic Hamming Error-Correcting Code (n=7, k=4)

Generator polynomial: g(x) = x^3 + x + 1  (binary: 1011)
"""

import math
from itertools import combinations  # noqa: F401 — available for future use


# ─────────────────────────────────────────────────────────────────────────────
# Polynomial arithmetic over GF(2)
# ─────────────────────────────────────────────────────────────────────────────

def poly_degree(poly: int) -> int:
    """Return the degree of a polynomial represented as an integer."""
    return poly.bit_length() - 1


def poly_mod(dividend: int, divisor: int) -> int:
    """
    Compute dividend mod divisor over GF(2) (XOR-based long division).
    Both polynomials are represented as integers (bit vectors).
    """
    deg_div = poly_degree(divisor)
    while True:
        deg_dd = poly_degree(dividend)
        if deg_dd < deg_div:
            break
        dividend ^= divisor << (deg_dd - deg_div)
    return dividend


def poly_to_str(poly: int, degree: int) -> str:
    """Return a human-readable polynomial string."""
    if poly == 0:
        return "0"
    terms = []
    for i in range(degree, -1, -1):
        if poly & (1 << i):
            if i == 0:
                terms.append("1")
            elif i == 1:
                terms.append("x")
            else:
                terms.append(f"x^{i}")
    return " + ".join(terms)


def int_to_bits(value: int, length: int) -> list:
    """Convert integer to a list of bits (MSB first) with given length."""
    return [(value >> (length - 1 - i)) & 1 for i in range(length)]


def bits_to_int(bits: list) -> int:
    """Convert list of bits (MSB first) to integer."""
    result = 0
    for b in bits:
        result = (result << 1) | b
    return result


def bits_to_str(bits: list) -> str:
    return "".join(str(b) for b in bits)


# ─────────────────────────────────────────────────────────────────────────────
# Cyclic Code class
# ─────────────────────────────────────────────────────────────────────────────

class CyclicCode:
    """
    Systematic cyclic code with:
        n — codeword length
        k — information bits
        r — parity bits (r = n - k)
        g — generator polynomial (integer representation)

    Default: (7, 4) Hamming cyclic code, g(x) = x^3 + x + 1  (0b1011 = 11)
    """

    def __init__(self, n: int = 7, k: int = 4, generator: int = 0b1011):
        self.n = n
        self.k = k
        self.r = n - k
        self.g = generator
        self._validate()

    def _validate(self):
        assert self.r == poly_degree(self.g), (
            f"Generator degree {poly_degree(self.g)} must equal r={self.r}"
        )

    # ── Encoding ──────────────────────────────────────────────────────────────

    def encode(self, info_bits: list) -> list:
        """
        Systematic encoding:
            codeword = [info_bits | parity_bits]
        Steps:
            1. Shift info polynomial left by r positions: m(x) * x^r
            2. Compute remainder: b(x) = m(x)*x^r mod g(x)
            3. Codeword: c(x) = m(x)*x^r + b(x)
        """
        if len(info_bits) != self.k:
            raise ValueError(f"Expected {self.k} information bits, got {len(info_bits)}")

        m = bits_to_int(info_bits)
        shifted = m << self.r                     # m(x) * x^r
        remainder = poly_mod(shifted, self.g)     # b(x)
        codeword_int = shifted ^ remainder        # c(x) = m(x)*x^r XOR b(x)
        return int_to_bits(codeword_int, self.n)

    # ── Syndrome ──────────────────────────────────────────────────────────────

    def syndrome(self, received: list) -> int:
        """
        Compute syndrome S = r(x) mod g(x).
        S == 0  ⟹ no error (or undetectable error).
        """
        r_int = bits_to_int(received)
        return poly_mod(r_int, self.g)

    # ── Syndrome table ────────────────────────────────────────────────────────

    def _build_syndrome_table(self) -> dict:
        """
        Build a lookup table: syndrome_value → error bit position (0-indexed from MSB).
        Single-bit error patterns only (corrects 1 error).
        """
        table = {}
        for pos in range(self.n):
            error = 1 << (self.n - 1 - pos)   # single-bit error at position pos
            s = poly_mod(error, self.g)
            table[s] = pos
        return table

    # ── Decoding ──────────────────────────────────────────────────────────────

    def decode(self, received: list, verbose: bool = True) -> dict:
        """
        Decode a received codeword.

        Returns a dict with all intermediate values:
            received        — received bits
            syndrome        — syndrome value
            syndrome_bits   — syndrome as bit list
            syndrome_poly   — syndrome polynomial string
            error_detected  — bool
            error_position  — bit index (0-based from MSB) or None
            corrected       — corrected codeword bits
            info_bits       — extracted information bits
            success         — True if error was corrected (or no error)
        """
        if len(received) != self.n:
            raise ValueError(f"Expected {self.n} bits, got {len(received)}")

        s = self.syndrome(received)
        s_bits = int_to_bits(s, self.r)
        s_poly = poly_to_str(s, self.r - 1)

        result = {
            "received": list(received),
            "syndrome": s,
            "syndrome_bits": s_bits,
            "syndrome_poly": s_poly,
            "error_detected": s != 0,
            "error_position": None,
            "corrected": list(received),
            "info_bits": None,
            "success": True,
        }

        if s != 0:
            table = self._build_syndrome_table()
            if s in table:
                pos = table[s]
                result["error_position"] = pos
                corrected = list(received)
                corrected[pos] ^= 1          # flip the erroneous bit
                result["corrected"] = corrected
                result["success"] = True
            else:
                # Multiple errors — cannot correct
                result["success"] = False

        result["info_bits"] = result["corrected"][:self.k]

        if verbose:
            self._print_decode_report(result)

        return result

    # ── Pretty-print decode report ────────────────────────────────────────────

    def _print_decode_report(self, r: dict):
        sep = "─" * 55
        print(sep)
        print("ДЕКОДИРОВАНИЕ / DECODING REPORT")
        print(sep)
        print(f"  Принятая комбинация (received) : {bits_to_str(r['received'])}")
        print(f"  Синдром (syndrome bits)         : {bits_to_str(r['syndrome_bits'])}"
              f"  = {r['syndrome']}  [{r['syndrome_poly']}]")

        if not r["error_detected"]:
            print("  Ошибок не обнаружено ✓")
        else:
            print("  ⚠ Ошибка обнаружена!")
            if r["error_position"] is not None:
                pos = r["error_position"]
                print(f"  Номер искажённого бита          : {pos + 1}  "
                      f"(индекс {pos}, считая слева)")
                print(f"  Исправленная комбинация         : {bits_to_str(r['corrected'])}")
            else:
                print("  Невозможно исправить (кратность ошибки > t)")

        print(f"  Информационные биты (extracted) : {bits_to_str(r['info_bits'])}")
        print(sep)

    # ── Code parameters ───────────────────────────────────────────────────────

    def code_distance(self) -> int:
        """
        Compute minimum Hamming distance by exhaustive search over all codewords.
        d_min = min weight of all non-zero codewords (linear code property).
        """
        min_d = self.n + 1
        for m_val in range(1, 1 << self.k):
            info = int_to_bits(m_val, self.k)
            cw = self.encode(info)
            w = sum(cw)
            if w < min_d:
                min_d = w
        return min_d

    def max_detectable_errors(self) -> int:
        """Maximum number of detectable errors: d_min - 1."""
        return self.code_distance() - 1

    def max_correctable_errors(self) -> int:
        """Maximum number of correctable errors: floor((d_min - 1) / 2)."""
        return (self.code_distance() - 1) // 2


# ─────────────────────────────────────────────────────────────────────────────
# Bounds
# ─────────────────────────────────────────────────────────────────────────────

def hamming_bound(n: int, k: int, t: int):
    """
    Hamming (sphere-packing) bound:
        2^(n-k) >= sum_{i=0}^{t} C(n, i)
    Returns (satisfied, parity_volume, sphere_sum).
    """
    parity_volume = 1 << (n - k)
    sphere_sum = sum(math.comb(n, i) for i in range(t + 1))
    return parity_volume >= sphere_sum, parity_volume, sphere_sum


def plotkin_bound(n: int, k: int, d: int):
    """
    Plotkin bound (applicable when 2d > n):
        2^k <= 2 * floor(d / (2*d - n))
    Returns (satisfied, bound_value) or (None, None) when not applicable.
    """
    if 2 * d <= n:
        return None, None
    bound = 2 * math.floor(d / (2 * d - n))
    return (1 << k) <= bound, bound


def varshamov_gilbert_bound(n: int, k: int, d: int):
    """
    Varshamov-Gilbert bound:
        sum_{i=0}^{d-2} C(n-1, i) < 2^(n-k)
    Returns (satisfied, lhs, rhs).
    """
    lhs = sum(math.comb(n - 1, i) for i in range(d - 1))
    rhs = 1 << (n - k)
    return lhs < rhs, lhs, rhs


def print_bounds(n: int, k: int, d: int, t: int):
    sep = "─" * 55
    print(sep)
    print("ГРАНИЦЫ / CODING BOUNDS")
    print(sep)

    # Hamming bound
    ok, pv, ss = hamming_bound(n, k, t)
    status = "✓ выполняется" if ok else "✗ не выполняется"
    print(f"  Граница Хэмминга (Hamming bound):")
    print(f"    2^(n-k) = 2^{n-k} = {pv}  >=  Σ C({n},i) [i=0..{t}] = {ss}")
    print(f"    {pv} >= {ss}  →  {status}")
    print()

    # Plotkin bound
    ok_p, bnd_p = plotkin_bound(n, k, d)
    if ok_p is None:
        print(f"  Граница Плоткина (Plotkin bound):")
        print(f"    Не применима (требуется 2d > n, т.е. {2*d} > {n})")
    else:
        status_p = "✓ выполняется" if ok_p else "✗ не выполняется"
        print(f"  Граница Плоткина (Plotkin bound):")
        print(f"    2^k = 2^{k} = {1 << k}  <=  2*floor(d/(2d-n)) = {bnd_p}")
        print(f"    {1 << k} <= {bnd_p}  →  {status_p}")
    print()

    # Varshamov-Gilbert bound
    ok_vg, lhs_vg, rhs_vg = varshamov_gilbert_bound(n, k, d)
    status_vg = "✓ выполняется" if ok_vg else "✗ не выполняется"
    print(f"  Граница Варшамова-Гильберта (Varshamov-Gilbert):")
    print(f"    Σ C({n-1},i) [i=0..{d-2}] = {lhs_vg}  <  2^(n-k) = {rhs_vg}")
    print(f"    {lhs_vg} < {rhs_vg}  →  {status_vg}")
    print(sep)


# ─────────────────────────────────────────────────────────────────────────────
# Test examples
# ─────────────────────────────────────────────────────────────────────────────

def run_tests(codec: CyclicCode):
    sep = "═" * 55
    print(sep)
    print("ТЕСТОВЫЕ ПРИМЕРЫ / TEST EXAMPLES")
    print(sep)

    test_cases = [
        ([1, 0, 1, 1], "no error"),
        ([1, 1, 0, 0], "no error"),
        ([0, 0, 0, 1], "no error"),
        ([1, 0, 1, 1], "error at bit 1"),
        ([1, 1, 0, 0], "error at bit 4"),
        ([0, 1, 1, 0], "error at bit 7"),
    ]

    error_positions = {
        "no error": None,
        "error at bit 1": 0,
        "error at bit 4": 3,
        "error at bit 7": 6,
    }

    for idx, (info, scenario) in enumerate(test_cases, 1):
        print(f"\n--- Тест {idx}: info={bits_to_str(info)}, сценарий='{scenario}' ---")
        print(f"  Информационные биты : {bits_to_str(info)}")

        # Encode
        codeword = codec.encode(info)
        print(f"  Кодовая комбинация  : {bits_to_str(codeword)}")

        # Inject error if needed
        received = list(codeword)
        err_pos = error_positions[scenario]
        if err_pos is not None:
            received[err_pos] ^= 1
            print(f"  Внесена ошибка в бит {err_pos + 1}: {bits_to_str(received)}")

        # Decode
        codec.decode(received, verbose=True)


# ─────────────────────────────────────────────────────────────────────────────
# Interactive CLI
# ─────────────────────────────────────────────────────────────────────────────

def parse_bits(s: str) -> list:
    """Parse a string of '0' and '1' characters into a list of ints."""
    s = s.strip().replace(" ", "")
    if not all(c in "01" for c in s):
        raise ValueError("Строка должна содержать только 0 и 1")
    return [int(c) for c in s]


def interactive_menu(codec: CyclicCode):
    sep = "═" * 55
    while True:
        print(f"\n{sep}")
        print("МЕНЮ / MENU")
        print(sep)
        print("  1. Параметры кода и границы")
        print("  2. Кодировать сообщение")
        print("  3. Декодировать кодовую комбинацию")
        print("  4. Кодировать + внести ошибку + декодировать")
        print("  5. Запустить тестовые примеры")
        print("  0. Выход")
        print(sep)
        choice = input("Выбор: ").strip()

        if choice == "0":
            print("До свидания!")
            break

        elif choice == "1":
            print_code_info(codec)

        elif choice == "2":
            raw = input(f"  Введите {codec.k} информационных бита (напр. 1011): ")
            try:
                bits = parse_bits(raw)
                if len(bits) != codec.k:
                    print(f"  Ошибка: нужно ровно {codec.k} бит.")
                    continue
                cw = codec.encode(bits)
                print(f"  Информационные биты : {bits_to_str(bits)}")
                print(f"  Кодовая комбинация  : {bits_to_str(cw)}")
                print(f"  Проверочные биты    : {bits_to_str(cw[codec.k:])}")
            except ValueError as e:
                print(f"  Ошибка ввода: {e}")

        elif choice == "3":
            raw = input(f"  Введите {codec.n} бит кодовой комбинации: ")
            try:
                bits = parse_bits(raw)
                if len(bits) != codec.n:
                    print(f"  Ошибка: нужно ровно {codec.n} бит.")
                    continue
                codec.decode(bits, verbose=True)
            except ValueError as e:
                print(f"  Ошибка ввода: {e}")

        elif choice == "4":
            raw_info = input(f"  Введите {codec.k} информационных бита: ")
            try:
                info = parse_bits(raw_info)
                if len(info) != codec.k:
                    print(f"  Ошибка: нужно ровно {codec.k} бит.")
                    continue
                cw = codec.encode(info)
                print(f"  Кодовая комбинация  : {bits_to_str(cw)}")
                raw_err = input(
                    f"  Введите номер бита для внесения ошибки (1–{codec.n}), "
                    f"или 0 для пропуска: "
                )
                err_pos_in = int(raw_err.strip())
                received = list(cw)
                if 1 <= err_pos_in <= codec.n:
                    received[err_pos_in - 1] ^= 1
                    print(f"  После ошибки в бите {err_pos_in}: {bits_to_str(received)}")
                codec.decode(received, verbose=True)
            except ValueError as e:
                print(f"  Ошибка ввода: {e}")

        elif choice == "5":
            run_tests(codec)

        else:
            print("  Неверный выбор. Попробуйте снова.")


def print_code_info(codec: CyclicCode):
    sep = "─" * 55
    d = codec.code_distance()
    t = codec.max_correctable_errors()
    det = codec.max_detectable_errors()

    print(sep)
    print("ПАРАМЕТРЫ КОДА / CODE PARAMETERS")
    print(sep)
    print(f"  Длина кодового слова         n = {codec.n}")
    print(f"  Количество информ. бит       k = {codec.k}")
    print(f"  Количество проверочных бит   r = {codec.r}")
    print(f"  Порождающий полином  g(x) = {poly_to_str(codec.g, codec.r)}")
    print(f"                       g    = {bin(codec.g)}  ({codec.g})")
    print(f"  Кодовое расстояние       d_min = {d}")
    print(f"  Макс. обнаруж. ошибок    t_det = {det}  (d-1)")
    print(f"  Макс. исправл. ошибок    t_cor = {t}    (floor((d-1)/2))")
    print()
    print_bounds(codec.n, codec.k, d, t)


# ─────────────────────────────────────────────────────────────────────────────
# Entry point
# ─────────────────────────────────────────────────────────────────────────────

def main():
    print("╔══════════════════════════════════════════════════════╗")
    print("║   Помехоустойчивое кодирование — Циклический код     ║")
    print("║   Noise-Resistant Coding: Cyclic (Hamming) Code      ║")
    print("║   (n=7, k=4)  g(x) = x^3 + x + 1                   ║")
    print("╚══════════════════════════════════════════════════════╝")

    # Default codec: (7,4) Hamming cyclic code
    codec = CyclicCode(n=7, k=4, generator=0b1011)

    # Print parameters on startup
    print_code_info(codec)

    # Launch interactive menu
    interactive_menu(codec)


if __name__ == "__main__":
    main()
