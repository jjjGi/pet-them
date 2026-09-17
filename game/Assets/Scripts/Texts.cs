using System;
using PetThem.Combat;

namespace PetThem.Game
{
    public enum Language { Korean, English }

    /// <summary>
    /// Every string the player reads, in each language the game ships.
    /// </summary>
    /// <remarks>
    /// Kept out of the combat core on purpose: the shared rules and the .NET simulator should not
    /// carry a user-facing language at all. Adding a language means adding a case here and nothing
    /// else, and the screen code never holds a literal, so nothing can be left untranslated by
    /// accident.
    ///
    /// Percentages in the upgrade text are of the run's starting value, not the current one, which
    /// is how CombatWorld.ChooseUpgrade applies them.
    /// </remarks>
    public static class Texts
    {
        public static Language Current { get; private set; } = Language.Korean;

        /// <summary>Starts in the device's language when we ship it, and in English otherwise.</summary>
        /// <param name="korean">True when the device is set to Korean. Passed in so this file
        /// stays free of UnityEngine and the translations can be checked without the editor.</param>
        public static void UseSystemLanguage(bool korean) =>
            Current = korean ? Language.Korean : Language.English;

        public static void Use(Language language) => Current = language;

        public static Language Next => Current == Language.Korean ? Language.English : Language.Korean;

        public static string LanguageName(Language language) =>
            language == Language.Korean ? "한국어" : "English";

        private static string Pick(string korean, string english) =>
            Current == Language.Korean ? korean : english;

        // The game's name is a brand and stays as it is in every language.
        public const string GameTitle = "PET THEM!";

        public static string Weapon(WeaponId id)
        {
            switch (id)
            {
                case WeaponId.Arrow: return Pick("화살", "ARROW");
                case WeaponId.Laser: return Pick("레이저", "LASER");
                case WeaponId.Punch: return Pick("펀치", "PUNCH");
                default: return id.ToString();
            }
        }

        public static string WeaponHint(WeaponId id)
        {
            switch (id)
            {
                case WeaponId.Arrow: return Pick("당겼다가 놓으면 발사, 많이 당길수록 세게", "pull back and let go, the further the stronger");
                case WeaponId.Laser: return Pick("누르는 동안 계속, 열 주의", "hold to burn, watch the heat");
                case WeaponId.Punch: return Pick("탭으로 휘두름, 끌어서 조준", "tap to swing, drag to aim");
                default: return id.ToString();
            }
        }

        public static string Pet(PetId id)
        {
            switch (id)
            {
                case PetId.Bori: return Pick("보리", "BORI");
                case PetId.Coco: return Pick("코코", "COCO");
                case PetId.Mochi: return Pick("모찌", "MOCHI");
                default: return id.ToString();
            }
        }

        public static string PetRole(PetId id)
        {
            switch (id)
            {
                case PetId.Bori:
                    return Pick("문 적을 얼리고 밀쳐냅니다", "chills and shoves what it bites");
                case PetId.Coco:
                    return Pick("체력을 채워주고 맞는 아픔을 줄여줍니다", "heals you and softens every hit");
                case PetId.Mochi:
                    return Pick("잔재주 없이 제일 세게 때립니다", "hits hardest, no tricks");
                default: return id.ToString();
            }
        }

        public static string UpgradeTitle(UpgradeId id)
        {
            switch (id)
            {
                case UpgradeId.PunchPower: return Pick("묵직한 손", "HEAVY HANDS");
                case UpgradeId.PunchReach: return Pick("긴 팔", "BIG HIGH FIVE");
                case UpgradeId.ArrowPower: return Pick("날카로운 촉", "SHARP TIP");
                case UpgradeId.ArrowPierce: return Pick("꿰뚫기", "THROUGH AND THROUGH");
                case UpgradeId.LaserPower: return Pick("뜨거운 빔", "HOT BEAM");
                case UpgradeId.LaserCooling: return Pick("차가운 머리", "COOL HEAD");
                case UpgradeId.PetPower: return Pick("튼튼한 이빨", "STRONG BITE");
                case UpgradeId.PetHaste: return Pick("신난 발걸음", "EAGER BUDDY");
                case UpgradeId.PetReach: return Pick("긴 목줄", "LONGER LEASH");
                case UpgradeId.PetChill: return Pick("차가운 코", "COLD NOSE");
                case UpgradeId.PetGuard: return Pick("푹신한 방석", "SOFT PILLOW");
                case UpgradeId.MoveSpeed: return Pick("빠른 발", "QUICK PAWS");
                case UpgradeId.Vitality: return Pick("커진 심장", "MORE HEART");
                case UpgradeId.Heal: return Pick("잠깐 쉬기", "PET BREAK");
                default: return id.ToString();
            }
        }

        public static string UpgradeDescription(UpgradeId id)
        {
            switch (id)
            {
                case UpgradeId.PunchPower:
                    return Pick("펀치 피해가 처음 위력의 20%만큼 늘어납니다.",
                        "Punch damage +20% of starting power.");
                case UpgradeId.PunchReach:
                    return Pick("펀치가 처음 사거리의 10%만큼 더 닿습니다.\n최대 5단계.",
                        "Punch reach +10% of starting range.\nUp to 5 ranks.");
                case UpgradeId.ArrowPower:
                    return Pick("화살 피해가 처음 위력의 22%만큼 늘어납니다.",
                        "Arrow damage +22% of starting power.");
                case UpgradeId.ArrowPierce:
                    return Pick("화살이 적을 하나 더 꿰뚫습니다.\n최대 5단계.",
                        "Each arrow pierces one more enemy.\nUp to 5 ranks.");
                case UpgradeId.LaserPower:
                    return Pick("레이저 초당 피해가 처음 위력의 18%만큼 늘어납니다.",
                        "Laser damage +18% of starting power per second.");
                case UpgradeId.LaserCooling:
                    return Pick("레이저가 10% 천천히 달아오르고 더 빨리 식습니다.\n최대 5단계.",
                        "Laser heats 10% slower and cools faster.\nUp to 5 ranks.");
                case UpgradeId.PetPower:
                    return Pick("펫 피해가 처음 위력의 25%만큼 늘어납니다.",
                        "Pet damage +25% of starting power.");
                case UpgradeId.PetHaste:
                    return Pick("펫 공격 간격이 처음 간격의 10%만큼 짧아집니다.\n최대 5단계.",
                        "Pet attack interval -10% of starting interval.\nUp to 5 ranks.");
                case UpgradeId.PetReach:
                    return Pick("모찌가 12% 더 멀리까지 닿습니다.\n최대 5단계.",
                        "Mochi reaches 12% further.\nUp to 5 ranks.");
                case UpgradeId.PetChill:
                    return Pick("보리의 냉기가 더 세지고 더 오래갑니다.\n최대 5단계.",
                        "Bori's chill is stronger and lasts longer.\nUp to 5 ranks.");
                case UpgradeId.PetGuard:
                    return Pick("코코가 더 많이 막아주고 더 많이 회복시킵니다.\n최대 5단계.",
                        "Coco blocks more contact damage and heals more.\nUp to 5 ranks.");
                case UpgradeId.MoveSpeed:
                    return Pick("이동 속도가 처음 속도의 8%만큼 빨라집니다.\n최대 5단계.",
                        "Move speed +8% of starting speed.\nUp to 5 ranks.");
                case UpgradeId.Vitality:
                    return Pick("최대 체력이 처음 체력의 20%만큼 늘고\n그만큼 지금 회복합니다.",
                        "Max health +20% of starting health.\nRestore the same amount now.");
                case UpgradeId.Heal:
                    return Pick("지금 최대 체력의 40%를 회복합니다.",
                        "Restore 40% of your current max health.");
                default: return id.ToString();
            }
        }

        // Heads-up display
        public static string Level(int level, bool maxed, int experience, int needed) =>
            maxed
                ? Pick($"레벨 {level}  /  최대", $"LEVEL {level}  /  MAX")
                : Pick($"레벨 {level}  /  {experience} / {needed} XP", $"LEVEL {level}  /  {experience} / {needed} XP");

        public static string WaveAndKills(int wave, int kills) =>
            Pick($"웨이브 {wave}   /   {kills} 처치", $"WAVE {wave}   /   {kills} KOs");

        public static string MoveHint => Pick("이동  /  WASD 또는 왼쪽 엄지", "MOVE  /  WASD or left thumb");
        public static string Heat => Pick("열", "HEAT");
        public static string Overheated => Pick("과열", "OVERHEATED");
        public static string Pause => Pick("II", "II");
        public static string Resume => Pick("계속", "PLAY");

        public static string BossHealth(int health) => Pick($"큰 놈  /  {health} HP", $"BIG ONE  /  {health} HP");
        public static string BossIncoming(int seconds) => Pick($"{seconds}초 뒤 큰 놈", $"BIG ONE IN {seconds}s");
        public static string GuardStatus(int percent, int seconds) =>
            Pick($"코코  /  피해 -{percent}%   /   {seconds}초 뒤 회복",
                $"COCO  /  -{percent}% contact   /   heal in {seconds}s");

        // Menus
        public static string StartHeadline => Pick("쓰다듬고, 두드려라.", "PET THEM!");
        public static string PausedHeadline => Pick("한숨 돌리는 중.", "TAKE A BREATHER.");
        public static string BossDownHeadline => Pick("큰 놈 잡았다!", "BIG ONE DOWN!");
        public static string WonHeadline => Pick("잘 쓰다듬었어.", "NICE PETTING.");
        public static string LostHeadline => Pick("한 번 더?", "ONE MORE PAT?");

        public static string StartBlurb =>
            Pick("3분을 버티세요. 2분에 큰 놈이 옵니다.\n무기와 펫을 고르고, 레벨마다 3개 중 하나를 선택합니다.",
                "Survive 3 minutes. A big one shows up at 2:00.\nPick a weapon and a buddy, then one of 3 upgrades per level.");

        public static string StartControls =>
            Pick("왼쪽 엄지로 이동, 오른쪽으로 공격합니다.\n펫은 알아서 싸웁니다.\nPC: WASD + 마우스. SPACE는 자동 조준.",
                "Move with your left thumb. The right side attacks.\nYour buddy fights on its own.\nDesktop: WASD + mouse. SPACE auto-targets.");

        public static string EnemyLegend =>
            Pick("코랄 = 쫓아오는 녀석. 골드 = 달려드는 녀석. 퍼플 = 덩치, 느리지만 아픕니다.\n핑크 = 큰 놈. 잡으면 시간이 남아도 끝납니다.",
                "Coral = chaser. Gold = runner. Purple = brute, slow but heavy.\nPink = the big one. Take it down to end the run early.");

        public static string RunResult(string time, int kills, int wave) =>
            Pick($"시간 {time}   /   {kills} 처치   /   웨이브 {wave}",
                $"Time {time}   /   {kills} KOs   /   Wave {wave}");

        public static string CoinsEarned(int earned, bool bossBonus, int saved) =>
            Pick($"+{earned} 코인{(bossBonus ? " (큰 놈 보너스)" : "")}   /   보유 {saved}",
                $"+{earned} COINS{(bossBonus ? " (big one bonus)" : "")}   /   {saved} saved");

        public static string WeaponPickerLabel => Pick("주무기  /  한 판 동안 바꿀 수 없습니다", "MAIN WEAPON  /  pick one for the whole run");
        public static string PetPickerLabel(PetId id) => Pick($"펫  /  {PetRole(id)}", $"BUDDY  /  {PetRole(id)}");

        public static string Play => Pick("시작하기  >", "LET'S PLAY  >");
        public static string KeepGoing => Pick("계속하기  >", "KEEP GOING  >");
        public static string TryAgain => Pick("다시 도전  >", "TRY AGAIN  >");
        public static string Restart => Pick("처음부터", "RESTART");
        public static string Shop(int coins) => Pick($"상점  /  {coins}", $"SHOP  /  {coins}");
        public static string Back => Pick("< 뒤로", "< BACK");
        public static string Build => Pick("프로토타입 0.9  /  무기 + 펫 + 보스 + 상점",
            "PROTOTYPE 0.9  /  WEAPONS + BUDDIES + BOSS + SHOP");
        public static string RunLog(string path) => Pick($"기록: {path}", $"Run log: {path}");

        // Level-up screen
        public static string LevelUpHeadline => Pick("레벨 업  /  다음 한 수를 고르세요", "LEVEL UP  /  CHOOSE YOUR NEXT PAT");
        public static string LevelUpHint => Pick("전투가 멈춰 있습니다. 하나만 고르세요. 키보드: 1 / 2 / 3.",
            "Combat is paused. Pick one card. Keyboard: 1 / 2 / 3.");
        public static string OptionRank(int option, int rank) =>
            Pick($"{option}번  /  {rank}단계", $"OPTION {option}  /  RANK {rank}");
        public static string PickOption(int option) => Pick($"{option}번 선택", $"PICK {option}");

        // Shop
        public static string ShopHeadline => Pick("펫 상점", "BUDDY SHOP");
        public static string ShopWallet(int coins, int runs) =>
            Pick($"{coins} 코인   /   {runs}판 완료", $"{coins} COINS   /   {runs} runs finished");
        public static string ShopHint =>
            Pick("코인은 처치, 버틴 시간, 큰 놈 처치로 모입니다. 중도 포기한 판은 주지 않습니다.",
                "Coins come from KOs, time survived and taking down the big one. Abandoned runs pay nothing.");
        public static string Starter => Pick("처음부터 함께", "YOURS FROM THE START");
        public static string Owned => Pick("보유 중", "OWNED");
        public static string Short(int price, int missing) =>
            Pick($"{price} 코인  /  {missing} 부족", $"{price} COINS  /  {missing} short");
        public static string Unlock(int price) => Pick($"해금  {price}", $"UNLOCK  {price}");
        public static string SavePath(string path) => Pick($"저장: {path}", $"Save: {path}");

        // Recording problems
        public static string RecordingUnavailable(string reason) =>
            Pick($"기록을 시작하지 못했습니다: {reason}", $"Recording unavailable: {reason}");
        public static string RecordingStopped(string reason) =>
            Pick($"기록이 중단됐습니다: {reason}", $"Recording stopped: {reason}");
    }
}
