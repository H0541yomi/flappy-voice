# PRD — Flappy Voice

## 1. Summary
Flappy Voice is a Flappy-Bird-style endless flyer where the player's singing
pitch — not taps — controls the character's height. Higher pitch = higher on
screen; crossing into the next octave wraps the character back to the bottom.
The player sings continuously to weave through a procedurally spawning field
of pipes, scoring one point per pipe cleared, until they collide and die. A
scripted attract-mode demo plays before input starts; singing into the mic
is what starts the run. On death the player sees their score and a share
action.

Target: casual mobile players, karaoke and rhythm-game fans, and
streamers/short-form creators looking for funny, clip-worthy vocal moments.

## 2. Problem & motivation
Flappy-style clones are a saturated genre almost universally built around
tap/touch input. There's no well-known entry that uses **live vocal pitch**
as the core mechanic, despite it being physically expressive, inherently
funny to watch (voice cracks = game over), and a natural fit for streaming
and short-form video. Flappy Voice's motivation is to take a mechanic
everyone already understands (Flappy Bird) and give it a novel, highly
shareable control scheme.

## 3. Goals / non-goals

**Goals (v1):**
- Real-time microphone pitch tracking mapped to character height, with the
  octave-wrap mechanic as the core skill element.
- A scripted attract-mode sequence that auto-flies the character through
  pipe gaps before the player sings, and hands off control seamlessly the
  moment voice input is detected.
- Endless procedural pipe generation with difficulty ramp (speed/gap size)
  over time.
- Score = number of pipes passed; local best score persisted on-device.
- End screen with final score and a "share to friends" action.

**Non-goals (v1):**
- Real-time multiplayer / duet voice battles.
- Melody- or song-matching scoring beyond raw pitch tracking.
- Cosmetic shop or monetization.
- A non-voice fallback control scheme (touch/tilt) — see Risks, section 9.

## 4. Users
Primary: casual mobile players and karaoke/rhythm-game fans looking for
a quick, funny "one more try" session. Secondary: streamers and short-form
creators (TikTok/Reels/Shorts) who want a game that produces inherently
entertaining clips of people singing badly at their phone.

## 5. Core data model

```
PlayerRun {
  runId: uuid
  playerId: string          // anonymous device/auth id
  score: int                // pipes passed
  durationSec: float
  timestamp: datetime
}

// v2 — global leaderboard
LeaderboardEntry {
  playerId: string
  displayName: string
  score: int
}

GameConfig {
  pipeGapSize: float
  pipeSpeed: float
  spawnIntervalSec: float
  difficultyRampCurve: AnimationCurve
  octaveRangeMinHz: float
  octaveRangeMaxHz: float
}

// runtime-only, never persisted:
PitchSample {
  timestampMs: long
  frequencyHz: float
  amplitude: float
  normalizedHeight: float   // 0..1 after octave-wrap mapping
}
```

## 6. Feature list

**v1 (must-have):**
1. Mic capture + real-time pitch detection (autocorrelation/YIN) with a
   noise gate and amplitude threshold to reject silence/noise.
2. Pitch → height mapping with octave-wrap: `height = (semitoneOffset mod 12) / 12`.
3. Attract mode: on load, the character auto-lerps between the Y-positions
   of upcoming pipe gaps (no mic needed); the moment sustained voiced pitch
   is detected, control hands off to the player at that same height (no
   snap/teleport).
4. Procedural pipe spawner with difficulty ramp (speed and/or gap size
   change with score or elapsed time).
5. Collision detection → death → end screen.
6. Score counter (pipes passed) + locally persisted best score.
7. End screen: final score, personal best, "Play again," and "Share."
8. Share: generate a shareable score card image and invoke the native
   share sheet.
9. Mic permission flow.

**v1.1 (nice-to-have):**
- Replace scripted attract-mode lerp with a ghost replay of the player's
  best run.
- Cosmetic skins for character/pipes.
- Sound-reactive visual effects synced to live pitch/amplitude.

**v2 (later):**
- Global leaderboard: backend, submit/fetch top scores, basic anti-cheat
  (rate limiting, plausible score-vs-duration cap), and an end-screen
  rank display.
- Duet/versus mode (two mics, or pass-the-device).
- Melody-matching bonus rounds.

## 7. Tech recommendation
**Engine:** Unity (2D URP template), C#.

**Pitch detection:** Unity's `Microphone` API feeding a custom DSP pitch
tracker (autocorrelation or YIN) — see section 11 for detail. This runs
fully on-device with no network dependency, which matters for latency.

**Leaderboard/auth backend:** none in v1 — score is local-only. When the
global leaderboard is built (v2), recommend Unity Gaming Services (UGS) —
Anonymous Authentication + Leaderboards service — over a custom backend:
it ships in a handful of SDK calls, has a free tier, and includes basic
score-submission safeguards out of the box.

**Share:** native share sheet via a Unity plugin (e.g. "Native Share" for
iOS/Android).

**Distribution:** hosted via Variant (a third-party platform that hosts
and distributes the game similarly to how Roblox hosts experiences),
rather than a direct App Store/Google Play listing — confirm Variant's
mic-permission and native-share API surface against the notes in section
11 before finalizing those integration points.

**Cost:** no backend infra to run or pay for in v1.

## 8. Success criteria
- Pitch-to-height input latency under ~100ms end to end (feels responsive,
  not laggy).
- Attract-mode successfully threads 100% of pipe gaps in playtesting
  (looks intentional, not glitchy).
- False-positive octave jumps in under 5% of sung notes during usability
  testing (mapping feels stable, not jittery).
- Full loop — attract → play → death → end screen → share — completes with
  zero crashes across 20 consecutive test runs.
- Median session includes 2+ runs (signals the "one more try" loop works).

## 9. Risks / decisions
- **Risk:** Mic pitch detection is unreliable in noisy environments.
  **Mitigation:** noise gate + amplitude threshold, require ~80ms of
  sustained pitch before registering a note change, and show an on-screen
  mic-level meter for player feedback.
- **Risk:** The octave-wrap mechanic confuses first-time players.
  **Mitigation:** attract mode doubles as an implicit tutorial; add a
  color-coded pitch meter showing octave bands during onboarding.
- **Risk:** Voice-only control excludes players who can't or won't sing
  aloud in public (accessibility/social-comfort gap).
  **Decision (v1):** accepted as the core differentiator; a touch/tilt
  fallback is explicitly out of scope for v1 and tracked as a v1.1/v2
  candidate, not silently dropped.
- **Risk (v2):** Leaderboard cheating via a prerecorded held tone, once a
  global leaderboard ships. **Mitigation:** server-side score-vs-duration
  sanity caps and submission rate limiting via UGS.
- **Decision:** no calibration step — assume every player can comfortably
  sing across at least one octave, and tune the fixed octave range in
  `GameConfig` to that assumption instead of measuring it per player.

## 10. Art style brainstorm
Four directions considered:

- **A. Neon Karaoke** — dark background, pipes rendered as glowing
  equalizer bars/speaker stacks, character a glowing music-note or singing
  blob, UI styled like a karaoke machine display. Mood: nightlife,
  high-contrast, streamer-friendly.
- **B. Flat Vector Pop** *(recommended for v1)* — bright flat-shaded 2D in
  the spirit of the original Flappy Bird, pipes redesigned as vintage
  organ pipes/speakers, character a cute bird with a tiny microphone,
  parallax sky that shifts hue per octave. Cheerful, broadly appealing,
  and the cheapest style to produce at readable sizes on mobile.
- **C. Papercraft/Cutout** — hand-cut paper look with soft drop shadows,
  pipes as folded paper megaphones, character a paper bird with
  sheet-music wings. Warm and tactile; visually distinct from every other
  flappy clone.
- **D. Retro 8-bit Chiptune** — pixel art, pipes as pixelated
  cassette/boombox stacks, screen-shake and a chiptune stinger on octave
  wrap. Nostalgic, pairs naturally with arcade high-score framing.

**Recommendation:** ship v1 in style **B** (Flat Vector Pop) — fastest to
produce, most legible at small mobile sizes, broadest appeal. Reserve
style A's neon palette as a "night mode" cosmetic skin for v1.1.

## 11. Unity implementation plan
- **Project setup:** Unity LTS, 2D URP template.
- **Mic input:** `Microphone.Start()` into a looping `AudioClip` buffer;
  read raw samples each frame via `AudioClip.GetData` over a
  1024–2048-sample window, updated ~30–60Hz.
- **Pitch detection:** implement the YIN algorithm (or autocorrelation) on
  that window — well-suited to monophonic voice and cheap enough to run
  every frame; prefer it over a pure-FFT approach, which is less accurate
  for a voice's fundamental frequency.
- **Hz → height mapping:** convert Hz to a MIDI note number via
  `69 + 12 * log2(f / 440)`, take `semitone mod 12` against the fixed
  range in `GameConfig`, normalize to 0–1, and use that as the target Y.
  The modulo operation *is* the octave-wrap.
- **Smoothing:** exponential moving average or `Mathf.SmoothDamp` on the
  target height to absorb vibrato jitter; clamp max speed so a voice crack
  can't teleport the character across the screen instantly.
- **Player movement:** kinematic `Rigidbody2D` moved via `MovePosition`
  toward the target height each `FixedUpdate` — direct positional mapping,
  not physics/gravity-driven like the original Flappy Bird's tap-to-flap.
- **State machine:** simple `GameState.Attract / Playing / GameOver` enum
  driving behavior. Both Attract and Playing feed the *same* "targetHeight"
  variable into the same movement code — only the source differs (a
  scripted curve sampling upcoming pipe-gap Y values vs. live pitch), so
  the handoff between them is just a swap of the input source, not a
  different code path.
- **Pipe spawner:** pooled objects (`Queue<GameObject>`) spawned at a fixed
  X interval, moving left at `pipeSpeed`; recycle off-screen; ramp
  difficulty via an `AnimationCurve` keyed on score or elapsed time.
- **Collision:** trigger colliders for "pipe passed" scoring, separate
  solid colliders for death.
- **Leaderboard (v2):** Unity Gaming Services SDK —
  `AuthenticationService.Instance.SignInAnonymouslyAsync()`,
  `LeaderboardsService.Instance.AddPlayerScoreAsync()`,
  `GetScoresAsync()`.
- **Share:** a native-share plugin invoking `UIActivityViewController`
  (iOS) / `Intent.ACTION_SEND` (Android); build the shared image by
  rendering the end-screen canvas to a `RenderTexture` and encoding to PNG
  at runtime.
- **Permissions:** `Application.RequestUserAuthorization(UserAuthorization.Microphone)`
  before the first attract-to-play transition.
- **Build targets:** mobile only, distributed via Variant rather than a
  direct App Store/Google Play listing — no browser/WebGL build, ever.
