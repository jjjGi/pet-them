# Clay Garden 아트 — 0.10.0

2026-09-18. 사용자 요청에 따라 기존 코드 도형을 새 캐릭터·배경 이미지로 교체했다.

## 적용된 에셋

경로: `game/Assets/Resources/Art/Clay/`.

| 파일 | 디자인 |
| --- | --- |
| Player.png | 크림색 고양이, 민트 장갑, 피치 스카프 |
| Mochi.png | 민트색 강아지, 처진 귀 |
| Bori.png | 파란 여우, 뾰족한 귀와 큰 꼬리 |
| Coco.png | 살구색 토끼, 긴 귀, 잎 목걸이 |
| Grunt.png | 코랄색 뿔 달린 꼬마 몬스터 |
| Runner.png | 골드색 길쭉한 돌진형 |
| Brute.png | 라벤더색 넓은 돌 골렘 |
| Boss.png | 라즈베리색 뿔 왕관 보스 |
| Arena.png | 넓게 비운 풀밭, 가장자리 돌·나무·꽃 |

제작 방법은 내장 **image_gen**이다. 입체적으로 조형된 질감의 **2D RGBA 스프라이트**이며,
Blender 원본·FBX 메시·리깅·다방향 애니메이션을 만든 것은 아니다.
캐릭터 원본 1254×1254, 배경 1659×948. 생성된 원본을 수정 없이 복사했다.

## Unity 적용과 성능 범위

- Art.Creature가 Resources/Art/Clay에서 이미지를 로드한다. 없을 때만 기존 절차적 도형으로 대체하며 경고한다.
- 한 생물당 SpriteRenderer 하나를 유지한다. 이미지 종류별 텍스처가 다르므로 드로우 콜 수가 생물 수와 같거나 항상 감소한다고 주장하지 않는다.
- ClayArtImporter: 캐릭터 최대 512px, 배경 최대 2048px, mipmap 끔, bilinear, clamp, alpha transparency, 읽기 불가, CompressedHQ.
- 실제 검증 임포트에서 8종 모두 512×512를 확인했다. Android 압축 결과·프레임은 별도 검증이 필요하다.
- Sprite.Create의 pixels-per-unit을 임포트 후 크기에 맞춰 캐릭터 캔버스를 1유닛으로 유지한다.
- 생성한 Sprite 래퍼만 파괴하고 Unity 소유의 임포트 텍스처는 파괴하지 않는다.
- 배경 테두리는 기존 전투 영역 밖에 배치한다. 충돌·전투 규칙·설정 수치는 변경하지 않았다.
- 시작 화면(넓은 가로 비율)과 펫 상점에 초상화를 추가했다.

## 확인한 화면

[캐릭터 모음](art/character-lineup.png) · [전투 크기 배치](art/arena-preview.png)

위 PNG는 **Unity에서 실제 임포트한 에셋을 렌더링한 검증 장면**이다.
사용자가 플레이한 화면이나 실제 전투 상황을 캡처한 것은 아니다. 배치 장면에는 비교를 위해 펫 3종을 함께 놓았다.
실제 게임에서는 선택한 펫 하나만 나온다. 원래 프로젝트를 닫지 않고 별도 검증용 복사본에서 실행했다.

- Unity 임포트·C# 컴파일·9개 이미지 로딩·8개 캐릭터 크기 검사 통과.
- 투명 배경과 실루엣, 배경 대비를 생성 이미지 및 Unity 렌더 PNG에서 눈으로 확인.
- 실제 Play에서 상점 글자와 초상화 간격, 적 100마리 식별, 모바일 프레임, 새 APK는 미검증.
- ClayArtValidation.Verify는 종료 시 에디터를 닫는다. **열어 둔 작업용 프로젝트에 실행하지 말고 별도 복사본에서만 배치 실행한다.**

## 사용한 최종 프롬프트 세트

내장 image_gen, 별도 API 키나 CLI 사용 없음. Player를 먼저 만들고 펫·몬스터에는 Player를 스타일 참고 이미지로 전달했다.
Brute는 참고 이미지 호출이 실패하여 아래 단독 프롬프트로 생성했다. Arena는 독립 생성이다.

### Player

Create a production game sprite for PET THEM!, a cute mobile 2D top-down survival action game. Asset: Player. One single full-body chibi cream-colored brave kitten adventurer with oversized soft mint boxing mittens, tiny warm peach scarf, dark teal button eyes and a small determined smile. Premium art-directed hand-modeled vinyl clay toy aesthetic, sculpted rounded forms, restrained fine material texture, soft upper-left studio light, clean crisp silhouette and soft ambient occlusion within the character. Camera orthographic front three-quarter slightly overhead (about 25 degrees), facing down toward viewer, neutral standing pose, feet visible, no weapon beyond mittens. Wide head, very short body, recognizable at 64 pixels. Entire character centered inside a square canvas, occupies about 80% of width and height, ample transparent margin. Genuine transparent RGBA background; no ground plane, no cast shadow outside character, no backdrop, no text, no labels, no frame, no other objects. Game-ready isolated sprite, polished appealing professional mobile game artwork. Palette cream ivory, mint, tiny peach accent, dark teal. This is the visual anchor for a coherent set of 8 clay creatures.

### Mochi / Bori / Coco / Grunt 공통 템플릿

Use the reference only for consistent art direction: premium soft sculpted vinyl/clay toy game art, pastel palette, dark teal glossy button eyes, subtle tactile material, soft upper-left lighting. Create a NEW distinct character sprite named {name}: {subject}. One single full body creature, orthographic front three-quarter slightly overhead about25degrees, facing down toward viewer, compact neutral stance, crisp readable silhouette at64px. Genuine transparent RGBA background. Entire creature centered with 10percent clear margins on all sides, roughly80percent canvas occupancy. No floor, no cast shadow outside creature, no environment, no text, no frame, no extra characters. Match the reference's professional visual quality and soft rounded modeling, not its cat identity.

- Mochi: mint-green round little puppy companion, floppy ears, cream muzzle, tiny peach collar, plump short legs, alert cheerful expression, no boxing gloves
- Bori: powder-blue chibi fox companion with distinct tall pointed ears, fluffy broad tail, small ivory muzzle, tiny icy blue collar gem, clever playful expression
- Coco: warm honey apricot chibi lop-eared rabbit companion, cream belly, peach blush, mint little leaf-shaped collar charm, nurturing sweet expression
- Grunt: coral red grumpy little bean monster with two stubby cream horns, broad squat body, tiny feet, dark teal brows, one tiny ivory fang, cute mischievous expression, no accessories

### Runner / Boss 공통 템플릿

Reference is STYLE ONLY, do not copy cat anatomy. Create one new full-body game creature {name}: {subject}. Match premium sculpted vinyl clay toy aesthetic, subtle tactile material, pastel colors, dark teal button eyes, soft upper-left studio light. Orthographic front three-quarter slightly overhead about25degrees, facing viewer, crisp distinctive silhouette readable64px. Character centered in square with10percent clear margins, full body occupies80percent. Genuine transparent RGBA background, no floor plane, no cast shadows outside body, no scenery, no text, no labels or extra objects. PET THEM! mobile survivor game monster sprite, professional cohesive visual quality.

- Runner: golden yellow speedy little imp, pointed teardrop-shaped head swept backwards, tall narrow silhouette, two tiny ivory side fins, dark teal mischievous eyes, short springy feet, forward ready-to-run stance
- Boss: raspberry pink giant chibi monster king, huge round powerful body, crown of five ivory crystal horns integrated into head, dark plum shoulder patches, dark teal fierce button eyes, ivory tiny tusks, giant rounded paws, playful fearsome boss silhouette

### Brute

One adorable lavender purple clay stone golem collectible toy as an isolated full body game sprite. Wide rounded square torso, chunky round arms, stubby little feet, small ivory rounded pebbles atop head, dark teal button eyes with mint button-stitch centers, simple friendly frown, cream belly patch. Premium tactile matte vinyl clay material, very soft upper-left studio lighting. Chibi proportions, orthographic front three-quarter view from slightly above. Entire toy centered in square image, fully visible with generous transparent margin. Actual transparent RGBA background, no floor, no backdrop, no external cast shadow, no text. Professional appealing pastel mobile game creature artwork.

### Arena

Create a finished 2D game arena background texture for PET THEM!, premium cute clay-toy forest playground mobile top-down survival action. Wide landscape 7:4 aspect ratio. Strict straight-down orthographic camera, no horizon and no perspective trapezoid. Flat wide rectangular muted sage-green grass clearing covers the central 90% of image: uncluttered low contrast desaturated mid-tone grass with tiny subtle tufts and sparse faint worn patches. Decorative rim limited to outermost 5% of image: rounded sculpted mossy stones, soft teal bushes, tiny cream flowers, occasional warm peach mushrooms, short rounded log sections. NO tall trees covering playable space, NO internal walls, NO paths forming grids, NO characters, NO pawprint icons, NO UI, NO text or labels. Soft modeled clay/vinyl diorama materials with restrained tactile detail, gentle light from upper left, coherent pastel art direction but floor significantly darker and less saturated than characters. Edge decorations small; entire central field stays a clearly playable unobstructed rectangle. Full bleed opaque texture. Professional polished game background, playable open grassy arena more than decorative illustration.