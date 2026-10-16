# 자료 조사: 피지컬과 인크리멘탈 (10-16)

> 사용자가 짚은 문제 넷을 기준으로, 비슷한 게임과 설계 이론을 모았다. 근본을 뒤집는 안도 포함.

## 출발점: 사용자가 짚은 문제

1. 손으로 하는 게임은 갈수록 **어려워져야** 하는데, 인크리멘탈은 갈수록 **쉬워진다**.
2. **볼거리가 부족하다.** 그리고 저글링은 2 → 3 → 4개가 +20%가 아니라 **배수로** 어려워진다.
3. **잣대가 하나가 아니다.** 깡통 수, 저글링 수, 체력이 다 같이 움직인다.
4. 저글링이 어려워서 **낙하를 느리게** 했더니 "떨어지는 걸 받아친다"가 "떠 있는 걸 맞힌다"로 바뀌어 **손맛이 죽었다.**

## 찾은 것: 다섯 가지 공통 원칙

### 1. 물리는 고정하고, 강해지는 건 "맞혔을 때 일어나는 일"
- **BALL x PIT** (2025, OpenCritic 86): 공의 튕김은 끝까지 같다. 바뀌는 건 공이 맞을 때 생기는 효과(불, 얼음, 번개, 공 두 개를 합치는 퓨전)뿐이다.
- **Idle Breakout**, **Footgun: Underground**도 같다. 관통, 분열, 폭발, 추적이 붙지만 낙하와 튕김은 안 건드린다.
- **Shelldiver**: 물리를 느리게 하지 않고, 같이 싸우는 것(연쇄 레이저, 폭발 복어)을 붙였다.
- 이론 (Gamasutra, "Player skill, character skill…"): 실력 게임에서 **하던 걸 쉽게 만드는 성장은 실력을 빼앗는다.** 낙하를 느리게 한 게 정확히 이 함정이다 → **문제 4**.
- **Celeste**: 도움을 주더라도 물리를 바꾸지 않고, 판정 창을 몰래 넓힌다(코요테 타임 등). **Peggle**의 "럭키 바운스"는 공이 사각지대로 빠지지 않게 방향을 몇 도만 튼다.

### 2. 어려워지는 건 "장소", 강해지는 건 "나"
- **Shelldiver**: 바이옴마다 새 해파리가 나온다. 강해지면 예전 바다는 쉬워지고(강해진 체감), 새 바다는 다시 어렵다.
- **Duck Hunt**: 라운드 합격선이 6 → 7 → 8 → 9 → 10마리로 오르고, 5라운드마다 오리가 빨라진다.
- **Balatro**: 앤티마다 목표 점수가 확 커지고, 나는 곱셈 조합으로 따라잡는다. **목표와 내 수치 사이의 간격이 긴장**이다.
- **Risk of Rain**: 시간 = 난이도. 오래 머물수록 강해지지만 적도 세진다.
- → **문제 1**: 쉬워지는 곳과 어려워지는 곳을 나누면 부딪치지 않는다.

### 3. 잣대 하나 = 숫자
- **Shoot The Barrels**: 통에 체력 숫자가 그대로 적혀 있고, 클수록 돈을 많이 준다. "예전엔 12번, 지금은 2번"이 바로 읽힌다.
- **Balatro**: "이번 판 목표 N점" 하나.
- → **문제 3**.

### 4. 실력은 생존 조건이 아니라 배율
- **Devil May Cry 스타일 랭크**: 생존 너머의 두 번째 성공 지표. 다양하게 칠수록 오르고, 가만히 있거나 맞으면 떨어진다.
- **Downwell**: 땅에 안 닿고 연속으로 죽이면 보상이 커진다. 한 동작이 공격, 이동, 체공을 다 맡는다(쏘면 떠 있는다, 땅에 닿아야 장전).
- → **문제 2·4**: 저글링을 못 해도 판은 굴러가고, 잘하면 크게 번다.

### 5. 볼거리는 연쇄와 결정적 순간에서
- **Shelldiver**: 가장 많이 칭찬받은 건 후반의 화면을 쓸어버리는 연쇄 폭발. 노드를 살 때마다 폭죽 연출.
- **Peggle**: 마지막 페그 앞에서 확대, 슬로모션, 드럼롤. 아깝게 빗나가도 나온다.
- "Juice it or lose it": 같은 벽돌깨기에 늘였다 줄이기, 파티클, 흔들림만 더해도 살아난다.
- → **문제 2**.

## 반면교사

- **Nodebuster**: 1위 불만이 "금방 너무 강해져서 시시하고, 후반은 켜 두기만 한다". → 마지막 현상금엔 강화로 못 지우는 조준 요구를 남긴다.
- **Peglin, Ballionaire**: "운이 실력보다 크다, 조준이 의미 없다". → 강화가 맞히는 걸 대신하면 안 된다.
- **Shelldiver, Nodebuster**: 숫자가 극단으로 커지면 화면이 어지럽고 섬광 때문에 눈이 피곤하다.
- **BALL x PIT**: 효과 색이 비슷해서 읽기 어렵다.

## 빈칸 (= 차별점일 수 있다)

- **업그레이드가 붙은 저글링 게임은 찾지 못했다.** Super Keepy Ups, Keepy Up, Juggle Star DX는 업그레이드가 없다.
- **서부 사격장 + 업그레이드도 찾지 못했다.** "쏴서 띄우는 저글링 사격"은 깡통만의 자리일 가능성이 높다.

## 뒤집기 아이디어 모음

| | 아이디어 | 근거 | 푸는 문제 |
|---|---|---|---|
| A | **1분 라운드, 떨어져도 책상으로 돌아온다, 공중 연속 맞힘 = 배율** (사용자 10-16 제안) | Shelldiver 산소, Downwell 콤보, DMC 스타일 | 1, 2, 4 |
| B | **강화는 맞힌 결과만**: 관통, 도탄, 파편이 옆 깡통을 띄움, 연쇄 폭발. 낙하·튕김 노드는 뺀다 | BALL x PIT, Idle Breakout, Footgun | 4, 볼거리 |
| C | **깡통 체력을 숫자로 크게 보여 주기**, 화력이 그걸 깎는다 | Shoot The Barrels, Balatro | 3 |
| D | **현상금 = 라운드 합격선** (덕헌트처럼 갈수록 오른다) | Duck Hunt, Balatro 앤티 | 1 |
| E | **현상금 계약을 난이도 다이얼로**: "중력 +20%", "4발만" 같은 조건을 걸수록 보상 증가 | Hades Heat | 1, 2 |
| F | **자정의 시계**: 한 판 안에서 밤이 깊어질수록 깡통이 빨라지고 많아진다 (악마 술집과 맞음) | Risk of Rain | 1 |
| G | **터뜨리면 한 발 회수**: 쏘기가 장전도 맡는다 | Downwell | 손맛 |
| H | **세 번째 깡통은 사건으로**: 계단을 숨기지 말고 "셋째 깡통 해금"을 크게 연출 | Duck Hunt A/B 모드 | 2 |
| I | **업그레이드에 대가**: "위력 2배, 대신 4발" | Downwell, BALL x PIT 캐릭터 | 1 |

※ "늘어난 깡통을 자동 보조 사격이 맡는다"(Idle Breakout, Shelldiver 도우미)도 나왔지만, 사용자가 10-16에 자동 사격은 컨셉과 안 맞는다고 해서 표에서 뺐다.

## 출처

- BALL x PIT: https://opencritic.com/game/19209/ball-x-p , https://www.useapotion.com/2025/11/ball-x-pit-review-bouncing-into-roguelite-perfection/ , https://mobalytics.gg/gamebase/reviews/ball-x-pit-review
- Peglin: https://www.thesixthaxis.com/2025/09/09/peglin-review/ , https://vaporlens.app/app/1296610/peglin
- Ballionaire: https://en.wikipedia.org/wiki/Ballionaire , https://rogueliker.com/ballionaire-early-access-review/
- Idle Breakout: https://www.crazygames.com/es/game/idle-breakout
- Footgun: https://rogueliker.com/footgun-underground-review/
- Downwell: https://en.wikipedia.org/wiki/Downwell_(video_game) , https://www.thumbsticks.com/gdc-2016-ojiro-fumoto-on-polishing-downwells-gun-boots/
- Peggle: https://www.gamesradar.com/peggle-10/ , https://nautil.us/how-designers-engineer-luck-into-video-games-236363
- Shelldiver: https://store.steampowered.com/app/3862670/Shelldiver/ , https://vaporlens.app/app/3862670/shelldiver.md , https://www.thexboxhub.com/shelldiver-review/
- Nodebuster: https://store.steampowered.com/app/3107330/Nodebuster/ , https://vaporlens.app/app/3107330/nodebuster.md
- Shoot The Barrels: https://store.steampowered.com/app/4332150/Shoot_The_Barrels/
- Duck Hunt: https://strategywiki.org/wiki/Duck_Hunt
- Juggle Star DX: https://dot-asterisk.itch.io/juggle-star-dx
- 실력 vs 성장: https://www.gamedeveloper.com/design/player-skill-character-skill-and-skill-progression-systems
- Hades God Mode: https://www.inverse.com/gaming/hades-god-mode-interview
- Risk of Rain: https://en.wikipedia.org/wiki/Risk_of_Rain
- Balatro: https://mp1st.com/news/balatro-update-1-07-shuffles-out-this-may-16
- Celeste: https://maddymakesgames.com/articles/celeste_and_forgiveness/index.html
- DMC 스타일 랭크: https://www.gamedeveloper.com/design/style-meters-and-play-aesthetics-in-death-of-a-wish
- 인크리멘탈 수학: https://code.tutsplus.com/numbers-getting-bigger-the-design-and-math-of-incremental-games--cms-24023a
