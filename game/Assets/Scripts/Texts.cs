using System;
using PetThem.Combat;

namespace PetThem.Game
{
    public enum Language { Korean, English }

    /// <summary>
    /// The sections of the home screen, in the order the tab bar shows them.
    /// </summary>
    /// <remarks>
    /// Declared next to the strings rather than beside the screen code because the screen code
    /// needs UnityEngine and this does not. Keeping it here is what lets the translation check
    /// walk every tab and prove each one has a name in each language.
    /// </remarks>
    public enum HomeTab { Home, Shop, Settings }

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

        /// <summary>
        /// The one place the build number lives. The editor stamps the Android package with it and
        /// the menu prints it, so the version on the phone and the version on screen cannot drift.
        /// </summary>
        public const string Version = "0.19.0";
        public static string MapPicker => Pick("탐험할 맵 · 끝없이 이동", "Explore · keep moving");
        public static string MapName(int theme) => theme == 1 ? Pick("모래 정원", "Dunes") :
            theme == 2 ? Pick("눈꽃 들판", "Snowfield") : Pick("초록 숲", "Woodland");
        public static string MusicSetting(bool enabled) => Pick("배경음악", "Music") + (enabled ? " ON" : " OFF");
        public static string SoundSetting(bool enabled) => Pick("효과음", "Sound") + (enabled ? " ON" : " OFF");

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
                case WeaponId.Arrow: return Pick("뒤로 당겼다 놓으면 앞으로 날아갑니다", "pull back and let go, it flies the other way");
                case WeaponId.Laser: return Pick("누르는 동안 계속, 열 주의", "hold to burn, watch the heat");
                case WeaponId.Punch: return Pick("탭으로 휘두름, 끌어서 조준", "tap to swing, drag to aim");
                default: return id.ToString();
            }
        }

        /// <summary>
        /// The coach's one line, for the lesson the player is currently failing to have learned.
        /// </summary>
        /// <remarks>
        /// Written as instructions rather than descriptions: this appears mid-fight, over the
        /// arena, and the player has a thumb on the screen. The attack lesson is per weapon because
        /// the three do not share a gesture -- the bow in particular cannot be guessed from any
        /// other game, which is why it also gets a second line of its own when a pull falls short.
        /// </remarks>
        public static string CoachLine(CoachLesson lesson, WeaponId weapon)
        {
            switch (lesson)
            {
                case CoachLesson.Move:
                    return Pick("왼쪽 화면에 엄지를 올리고 끌어 보세요",
                        "Put a thumb on the left side and drag");
                case CoachLesson.Attack:
                    switch (weapon)
                    {
                        case WeaponId.Arrow:
                            return Pick("오른쪽을 눌러 뒤로 당겼다 놓으세요. 당긴 반대쪽으로 날아갑니다",
                                "Press on the right, pull back, let go. It flies the way you did not pull");
                        case WeaponId.Laser:
                            return Pick("오른쪽을 누르고 있으면 빔이 나갑니다. 열이 100이면 잠깁니다",
                                "Hold on the right to burn. At 100 heat it locks until it cools");
                        case WeaponId.Punch:
                            return Pick("오른쪽을 탭해서 때리세요. 끌면 그 방향으로 휘두릅니다",
                                "Tap on the right to swing. Drag to swing that way");
                        default: return weapon.ToString();
                    }
                case CoachLesson.Draw:
                    return Pick("더 당기세요. 살짝 누르는 것은 발사가 아닙니다",
                        "Pull further. A tap is not a shot");
                case CoachLesson.Upgrade:
                    return Pick("고른 강화는 이번 판에만 남습니다. 일시정지에서 모아 볼 수 있습니다",
                        "Upgrades last this run only. The pause screen lists what you have");
                case CoachLesson.Boss:
                    return Pick("보스가 옵니다. 바닥에 도형이 뜨면 그 자리에서 비키세요",
                        "The boss is coming. When a shape lights the ground, leave that ground");
                default: return lesson.ToString();
            }
        }

        public static string CoachReplay =>
            Pick("조작 다시 배우기", "LEARN THE CONTROLS AGAIN");

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
                case UpgradeId.Drone: return Pick("꼬마 드론", "LITTLE DRONE");
                case UpgradeId.Orbit: return Pick("맴도는 구슬", "SPINNING BEADS");
                case UpgradeId.Crit: return Pick("급소 찌르기", "SWEET SPOT");
                case UpgradeId.Blast: return Pick("펑 터짐", "GOES POP");
                case UpgradeId.Lifesteal: return Pick("한 입 보충", "QUICK SNACK");
                case UpgradeId.Thorns: return Pick("따끔한 털", "PRICKLY COAT");
                case UpgradeId.Repel: return Pick("밀어내는 숨", "SHOCKWAVE");
                case UpgradeId.Regen: return Pick("스르르 회복", "SLOW MEND");
                case UpgradeId.Greed: return Pick("동전 욕심", "COIN GREED");
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
                case UpgradeId.Drone:
                    return Pick("드론이 하나 따라다니며\n당신이 고른 무기로 대신 쏩니다.\n최대 5기.",
                        "A drone tags along and fires\nthe weapon you picked.\nUp to 5.");
                case UpgradeId.Orbit:
                    return Pick("구슬이 주위를 돌며\n스치는 적을 때립니다.\n최대 5개.",
                        "A bead circles you and hits\nwhatever it brushes past.\nUp to 5.");
                case UpgradeId.Crit:
                    return Pick("가끔 급소에 맞아\n피해가 크게 늘어납니다.\n최대 5단계.",
                        "Some hits land just right\nand hurt a lot more.\nUp to 5 ranks.");
                case UpgradeId.Blast:
                    return Pick("쓰러진 적이 터져서\n주변 적에게 피해를 줍니다.\n최대 5단계.",
                        "A fallen enemy pops and\nhurts the ones around it.\nUp to 5 ranks.");
                case UpgradeId.Lifesteal:
                    return Pick("적을 쓰러뜨릴 때마다\n체력을 조금 회복합니다.\n최대 5단계.",
                        "Every enemy you drop\ngives a little health back.\nUp to 5 ranks.");
                case UpgradeId.Thorns:
                    return Pick("당신을 건드린 적이\n대신 아파합니다.\n최대 5단계.",
                        "Whatever touches you\ngets hurt for trying.\nUp to 5 ranks.");
                case UpgradeId.Repel:
                    return Pick("맞는 순간 주변 적을\n밀쳐냅니다. 피해는 없습니다.\n최대 5단계.",
                        "Being hit shoves the crowd\noff you. No damage.\nUp to 5 ranks.");
                case UpgradeId.Regen:
                    return Pick("가만히 있어도\n체력이 천천히 찹니다.\n최대 5단계.",
                        "Health creeps back up\non its own.\nUp to 5 ranks.");
                case UpgradeId.Greed:
                    return Pick("처치할 때 받는 코인이\n늘어납니다.\n최대 5단계.",
                        "Each KO is worth\nmore coins.\nUp to 5 ranks.");
                default: return id.ToString();
            }
        }

        public static string UpgradeKindName(UpgradeKind kind)
        {
            switch (kind)
            {
                case UpgradeKind.Weapon: return Pick("무기", "WEAPON");
                case UpgradeKind.Pet: return Pick("펫", "BUDDY");
                case UpgradeKind.Friend: return Pick("동료", "FRIEND");
                case UpgradeKind.Trigger: return Pick("발동", "TRIGGER");
                case UpgradeKind.Body: return Pick("몸", "BODY");
                default: return kind.ToString();
            }
        }

        // Pause screen: what this run has become
        public static string BuildHeadline => Pick("지금까지 고른 것", "WHAT YOU HAVE PICKED");
        public static string BuildSummary(string weapon, string pet, int level) =>
            Pick($"{weapon}  +  {pet}   /   레벨 {level}", $"{weapon}  +  {pet}   /   LEVEL {level}");
        public static string BuildEmpty =>
            Pick("아직 고른 강화가 없습니다. 적을 쓰러뜨리면 곧 고를 수 있습니다.",
                "Nothing picked yet. Drop a few enemies and the first card comes up.");
        public static string RankDots(int rank) => new string('●', rank) + new string('○', 5 - rank);

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

        /// <summary>Names the move the boss is winding up, so the shape on the ground has a word for it.</summary>
        public static string BossMove(BossAction move)
        {
            switch (move)
            {
                case BossAction.Charge: return Pick("돌진!", "CHARGE!");
                case BossAction.Slam: return Pick("내려찍기!", "SLAM!");
                case BossAction.Summon: return Pick("부른다!", "CALLING!");
                default: return move.ToString();
            }
        }

        public static string BossEnraged => Pick("격노", "ENRAGED");

        /// <summary>The name of the condition this run is played under.</summary>
        public static string TwistTitle(TwistId twist)
        {
            switch (twist)
            {
                case TwistId.Calm: return Pick("평범한 날", "AN ORDINARY DAY");
                case TwistId.Swarm: return Pick("우글우글", "SWARMING");
                case TwistId.Swift: return Pick("발 빠른 무리", "QUICK FEET");
                case TwistId.Armored: return Pick("단단한 껍질", "THICK HIDES");
                case TwistId.EarlyBoss: return Pick("이른 방문", "EARLY VISITOR");
                case TwistId.Harsh: return Pick("매서운 이빨", "SHARP TEETH");
                default: return twist.ToString();
            }
        }

        /// <summary>What the twist actually does, in the player's terms rather than in multipliers.</summary>
        public static string TwistDescription(TwistId twist)
        {
            switch (twist)
            {
                case TwistId.Calm:
                    return Pick("특별한 일 없는 하루입니다.", "Nothing out of the ordinary.");
                case TwistId.Swarm:
                    return Pick("훨씬 많이 몰려옵니다. 대신 하나하나는 약합니다.",
                        "Far more of them, and each one frailer.");
                case TwistId.Swift:
                    return Pick("전부 훨씬 빠릅니다. 도망칠 틈이 좁습니다.",
                        "All of them move much faster. Less room to run.");
                case TwistId.Armored:
                    return Pick("수는 적지만 좀처럼 쓰러지지 않습니다.",
                        "Fewer of them, and they take much longer to drop.");
                case TwistId.EarlyBoss:
                    return Pick("큰 놈이 훨씬 일찍 찾아옵니다. 준비할 시간이 모자랍니다.",
                        "The big one arrives far earlier, before your build is ready.");
                case TwistId.Harsh:
                    return Pick("맞으면 훨씬 아픕니다.", "Everything hits much harder.");
                default: return twist.ToString();
            }
        }

        public static string TwistReward => Pick("코인 추가", "bonus coins");
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

        public static string TabName(HomeTab tab)
        {
            switch (tab)
            {
                case HomeTab.Home: return Pick("홈", "HOME");
                case HomeTab.Shop: return Pick("상점", "SHOP");
                case HomeTab.Settings: return Pick("설정", "SETTINGS");
                default: return tab.ToString();
            }
        }

        /// <summary>The coin count in the home screen's top bar, where it is always in view.</summary>
        public static string Wallet(int coins) => Pick($"코인  {coins}", $"COINS  {coins}");

        public static string ToHome => Pick("홈으로", "HOME");

        /// <summary>How many finished runs are still waiting to reach the server.</summary>
        /// <remarks>
        /// Shown rather than hidden. A player who has been offline has earned those coins and is
        /// owed the knowledge that the game still has them.
        /// </remarks>
        public static string Unsent(int runs) =>
            runs == 0 ? Pick("전송할 기록 없음", "Nothing waiting to send")
                      : Pick($"보낼 판 {runs}개가 기다리는 중", $"{runs} finished runs waiting to send");

        public static string ServerOff => Pick("서버 연결 안 함 (혼자 플레이)", "No server (playing alone)");

        /// <summary>The address box, which is also how the server is turned off: leave it empty.</summary>
        public static string ServerAddress =>
            Pick("서버 주소  /  비워두면 보내지 않습니다", "Server address  /  empty means send nowhere");
        public static string ServerApply => Pick("적용", "APPLY");

        /// <summary>The rank band on a card. Cool to hot, the order these things are always in.</summary>
        public static string RankTier(int rank)
        {
            switch (rank)
            {
                case 1: return Pick("보통", "COMMON");
                case 2: return Pick("좋음", "FINE");
                case 3: return Pick("희귀", "RARE");
                case 4: return Pick("영웅", "EPIC");
                default: return Pick("전설", "LEGEND");
            }
        }

        /// <summary>Shown instead of the band when taking this card finishes it.</summary>
        public static string RankMax => Pick("최대", "MAX");

        public static string Reroll(int left) =>
            left > 0 ? Pick($"다시 돌리기  ({left})", $"RE-ROLL  ({left})")
                     : Pick("다시 돌리기 없음", "NO RE-ROLLS LEFT");

        /// <summary>
        /// Shown in place of the interface when drawing it threw.
        /// </summary>
        /// <remarks>
        /// Deliberately asks for the photograph. Whoever is holding the phone is the only one who
        /// can see this, and the message underneath is the whole diagnosis.
        /// </remarks>
        public static string ScreenBroke(string version) =>
            Pick($"화면을 그리다 문제가 생겼습니다 (버전 {version}).\n이 글자를 찍어서 보내 주세요. 게임은 계속 돌고 있습니다.",
                $"Something went wrong drawing the screen (version {version}).\nPlease photograph this. The game itself is still running.");
        public static string ScreenRetry => Pick("다시 그려보기", "TRY DRAWING AGAIN");

        // How the last upload went. A queue that does not move and says nothing is the worst of
        // both: something is wrong and nobody has been told what.
        public static string SendOk => Pick("마지막 전송: 성공", "Last send: went through");
        public static string SendUnreachable(string reason) =>
            Pick($"마지막 전송: 서버에 닿지 못함 ({reason})", $"Last send: could not reach the server ({reason})");
        public static string SendRefused(long status) =>
            Pick($"마지막 전송: 서버가 거절함 (HTTP {status})", $"Last send: the server said no (HTTP {status})");
        public static string SendNoSession =>
            Pick("마지막 전송: 세션을 받지 못함", "Last send: no session came back");
        public static string OutboxUnavailable(string reason) =>
            Pick($"기록 대기열을 저장하지 못했습니다: {reason}", $"Could not save the send queue: {reason}");
        public static string Loadout(WeaponId held, PetId buddy) => Weapon(held) + "  /  " + Pet(buddy);
        public static string InfoHeadline => Pick("이 빌드에 대하여", "ABOUT THIS BUILD");

        public static string Play => Pick("시작하기  >", "LET'S PLAY  >");
        public static string KeepGoing => Pick("계속하기  >", "KEEP GOING  >");
        public static string TryAgain => Pick("다시 도전  >", "TRY AGAIN  >");
        public static string Restart => Pick("처음부터", "RESTART");
        public static string Shop(int coins) => Pick($"상점  /  {coins}", $"SHOP  /  {coins}");
        public static string Back => Pick("< 뒤로", "< BACK");
        public static string Build => Pick(
            $"프로토타입 {Version}  /  이변 6종 + 강화 22종 + 무기 + 펫 + 보스",
            $"PROTOTYPE {Version}  /  6 TWISTS + 22 UPGRADES + WEAPONS + BUDDIES + BOSS");
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
