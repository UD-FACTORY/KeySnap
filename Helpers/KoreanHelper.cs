using System;
using System.Collections.Generic;
using System.Text;

namespace QuickReplace.Helpers
{
    public static class KoreanHelper
    {
        // 초성 (19개)
        private static readonly char[] ChoSung = {
            'ㄱ', 'ㄲ', 'ㄴ', 'ㄷ', 'ㄸ', 'ㄹ', 'ㅁ', 'ㅂ', 'ㅃ', 'ㅅ',
            'ㅆ', 'ㅇ', 'ㅈ', 'ㅉ', 'ㅊ', 'ㅋ', 'ㅌ', 'ㅍ', 'ㅎ'
        };

        // 중성 (21개)
        private static readonly char[] JungSung = {
            'ㅏ', 'ㅐ', 'ㅑ', 'ㅒ', 'ㅓ', 'ㅔ', 'ㅕ', 'ㅖ', 'ㅗ', 'ㅘ',
            'ㅙ', 'ㅚ', 'ㅛ', 'ㅜ', 'ㅝ', 'ㅞ', 'ㅟ', 'ㅠ', 'ㅡ', 'ㅢ', 'ㅣ'
        };

        // 종성 (28개, 0은 없음)
        private static readonly char[] JongSung = {
            '\0', 'ㄱ', 'ㄲ', 'ㄳ', 'ㄴ', 'ㄵ', 'ㄶ', 'ㄷ', 'ㄹ', 'ㄺ',
            'ㄻ', 'ㄼ', 'ㄽ', 'ㄾ', 'ㄿ', 'ㅀ', 'ㅁ', 'ㅂ', 'ㅄ', 'ㅅ',
            'ㅆ', 'ㅇ', 'ㅈ', 'ㅊ', 'ㅋ', 'ㅌ', 'ㅍ', 'ㅎ'
        };

        // 자모 -> 두벌식 영문 키 매핑
        private static readonly Dictionary<char, string> JamoToEnglish = new()
        {
            {'ㄱ', "r"}, {'ㄲ', "R"}, {'ㄳ', "rt"},
            {'ㄴ', "s"}, {'ㄵ', "sw"}, {'ㄶ', "sg"},
            {'ㄷ', "e"}, {'ㄸ', "E"},
            {'ㄹ', "f"}, {'ㄺ', "fr"}, {'ㄻ', "fa"}, {'ㄼ', "fq"}, {'ㄽ', "ft"}, {'ㄾ', "fx"}, {'ㄿ', "fv"}, {'ㅀ', "fg"},
            {'ㅁ', "a"},
            {'ㅂ', "q"}, {'ㅃ', "Q"}, {'ㅄ', "qt"},
            {'ㅅ', "t"}, {'ㅆ', "T"},
            {'ㅇ', "d"},
            {'ㅈ', "w"}, {'ㅉ', "W"},
            {'ㅊ', "c"},
            {'ㅋ', "z"},
            {'ㅌ', "x"},
            {'ㅍ', "v"},
            {'ㅎ', "g"},
            {'ㅏ', "k"}, {'ㅐ', "o"}, {'ㅑ', "i"}, {'ㅒ', "O"},
            {'ㅓ', "j"}, {'ㅔ', "p"}, {'ㅕ', "u"}, {'ㅖ', "P"},
            {'ㅗ', "h"}, {'ㅘ', "hk"}, {'ㅙ', "ho"}, {'ㅚ', "hl"},
            {'ㅛ', "y"},
            {'ㅜ', "n"}, {'ㅝ', "nj"}, {'ㅞ', "np"}, {'ㅟ', "nl"},
            {'ㅠ', "b"},
            {'ㅡ', "m"}, {'ㅢ', "ml"}, {'ㅣ', "l"}
        };

        // 영문 키 -> 두벌식 기본 자모 매핑
        private static readonly Dictionary<char, char> EnglishToJamo = new()
        {
            {'q', 'ㅂ'}, {'Q', 'ㅃ'}, {'w', 'ㅈ'}, {'W', 'ㅉ'}, {'e', 'ㄷ'}, {'E', 'ㄸ'},
            {'r', 'ㄱ'}, {'R', 'ㄲ'}, {'t', 'ㅅ'}, {'T', 'ㅆ'}, {'y', 'ㅛ'}, {'u', 'ㅕ'},
            {'i', 'ㅑ'}, {'o', 'ㅐ'}, {'O', 'ㅒ'}, {'p', 'ㅔ'}, {'P', 'ㅖ'}, {'a', 'ㅁ'},
            {'s', 'ㄴ'}, {'d', 'ㅇ'}, {'f', 'ㄹ'}, {'g', 'ㅎ'}, {'h', 'ㅗ'}, {'j', 'ㅓ'},
            {'k', 'ㅏ'}, {'l', 'ㅣ'}, {'z', 'ㅋ'}, {'x', 'ㅌ'}, {'c', 'ㅊ'}, {'v', 'ㅍ'},
            {'b', 'ㅠ'}, {'n', 'ㅜ'}, {'m', 'ㅡ'}
        };

        /// <summary>
        /// 한글 문자열(예: "ㅇㅈ", "인정", "ㄱㅅ")을 두벌식 영문 키스트로크("dw", "dls wjd", "rt")로 변환합니다.
        /// </summary>
        public static string DecomposeToKeyStrokes(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

            var sb = new StringBuilder();
            foreach (char ch in input)
            {
                // 1. 완성형 한글 (0xAC00 ~ 0xD7A3)
                if (ch >= 0xAC00 && ch <= 0xD7A3)
                {
                    int unicode = ch - 0xAC00;
                    int cho = unicode / (21 * 28);
                    int jung = (unicode % (21 * 28)) / 28;
                    int jong = unicode % 28;

                    char choChar = ChoSung[cho];
                    char jungChar = JungSung[jung];

                    if (JamoToEnglish.TryGetValue(choChar, out var choEng)) sb.Append(choEng);
                    if (JamoToEnglish.TryGetValue(jungChar, out var jungEng)) sb.Append(jungEng);

                    if (jong > 0)
                    {
                        char jongChar = JongSung[jong];
                        if (JamoToEnglish.TryGetValue(jongChar, out var jongEng)) sb.Append(jongEng);
                    }
                }
                // 2. 단독 자모 (0x3131 ~ 0x318E)
                else if (JamoToEnglish.TryGetValue(ch, out var eng))
                {
                    sb.Append(eng);
                }
                // 3. 일반 문자 (영어, 기호, 숫자 등)
                else
                {
                    sb.Append(ch);
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// 영문 단일 문자를 두벌식 자모로 변환 시도 (매핑 없을 시 그대로 반환)
        /// </summary>
        public static char ToKoreanJamo(char engChar)
        {
            return EnglishToJamo.TryGetValue(engChar, out char jamo) ? jamo : engChar;
        }

        /// <summary>
        /// 단축어 입력을 화면에서 깨끗하게 지우기 위해 필요한 정확한 백스페이스 횟수를 계산합니다.
        /// Windows 한글 입력기(IME)에서는 실시간 조합(Composition) 중인 마지막 음절을 지울 때 자모 단위로 지워지며,
        /// 이미 확정된 앞선 음절들은 글자(음절) 단위로 1회씩 지워집니다.
        /// </summary>
        public static int CalculateBackspaceCount(string shortcut)
        {
            if (string.IsNullOrEmpty(shortcut)) return 0;

            // 1. 앞선 글자들은 이미 완성/확정(Commit)되어 있으므로 글자당 1회씩 지워짐
            int total = shortcut.Length - 1;

            // 2. 마지막 글자는 현재 IME 조합(Composition) 상태에 머물러 있으므로 자모 타수만큼 백스페이스가 소모됨
            char lastChar = shortcut[^1];

            if (lastChar >= 0xAC00 && lastChar <= 0xD7A3)
            {
                int unicode = lastChar - 0xAC00;
                int jung = (unicode % (21 * 28)) / 28;
                int jong = unicode % 28;

                int lastSyllableKeys = 1; // 초성 (항상 1타)

                char jungChar = JungSung[jung];
                if (JamoToEnglish.TryGetValue(jungChar, out var jungEng))
                {
                    lastSyllableKeys += jungEng.Length; // 단모음 1타, 이중모음 2타
                }

                if (jong > 0)
                {
                    char jongChar = JongSung[jong];
                    if (JamoToEnglish.TryGetValue(jongChar, out var jongEng))
                    {
                        lastSyllableKeys += jongEng.Length; // 홑받침 1타, 겹받침 2타
                    }
                }

                total += lastSyllableKeys;
            }
            else if (JamoToEnglish.TryGetValue(lastChar, out var eng))
            {
                // 단독 자모인 경우 (예: 'ㄳ' -> 2타, 'ㅇ' -> 1타)
                total += eng.Length;
            }
            else
            {
                // 일반 문자 (영문, 숫자, 기호 등)는 1회
                total += 1;
            }

            return total;
        }
    }
}
