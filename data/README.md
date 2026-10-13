# data/

Inputs for the engine tests and the eval harness. Nothing here is loaded by the runtime app except
`words/` (bundled with the host from M2 on).

| Path | What | Source | Checked in? |
|---|---|---|---|
| `layouts/<langid>.json` | key→char table of each installed layout | `M0Spike dumpjson data/layouts` (this machine) | yes — small, machine-specific but stable for en-US / he-IL |
| `words/en_50k.txt` | English word frequency list, `word count` per line | [hermitdave/FrequencyWords](https://github.com/hermitdave/FrequencyWords) `content/2018/en/en_50k.txt` (OpenSubtitles-derived, CC-BY-SA 4.0) | yes — bundled with the app |
| `words/he_50k.txt` | Hebrew word frequency list, same format | same repo, `content/2018/he/he_50k.txt` | yes — bundled with the app |
| `corpus/eng_sentences.tsv` | English sentences for the eval harness, `id\tlang\ttext` | [Tatoeba per-language export](https://tatoeba.org/en/downloads) `eng_sentences.tsv.bz2` (CC-BY 2.0 FR) | no — download |
| `corpus/heb_sentences.tsv` | Hebrew sentences, same format | Tatoeba `heb_sentences.tsv.bz2` | no — download |

Tests tagged `[DataFact]` and the eval harness skip or refuse to run until `words/` and `corpus/` exist.
The word lists double as the engine's dictionaries, so their licence is part of the shipped product.

## Word lists at runtime

The host needs one list per installed keyboard language (`<code>_50k.txt`, `code` = FrequencyWords' folder name:
the two-letter ISO code, except `no` for Norwegian Bokmål/Nynorsk and `pt_br` for Brazilian Portuguese; none for
Japanese, Chinese and Korean, which are typed through an IME). It looks in two places, the first match winning:

1. `%LocalAppData%\AutoLang\words\` — lists it downloaded;
2. `data\words\` next to `AutoLang.exe` — lists bundled with the build.

A missing list is downloaded in the background (on by default; *Languages…* in the tray menu turns it off or
fetches one by hand) from
`https://raw.githubusercontent.com/hermitdave/FrequencyWords/525f9b560de45753a5ea01069454e72e9aa541c6/content/2018/<code>/<code>_50k.txt`.
The commit is pinned so upstream changes never reach users without a new AutoLang release. A download is written to
`.part`, capped at 8 MB, and only installed if it has at least 1,000 entries and its first 5,000 words are mostly in
the keyboard's alphabet. This is the app's only network use; nothing about the user is sent but the language code.

**Attribution.** Word lists: [FrequencyWords](https://github.com/hermitdave/FrequencyWords) by Hermit Dave, built from
OpenSubtitles 2018, licensed [CC BY-SA 4.0](https://creativecommons.org/licenses/by-sa/4.0/). Used unmodified. The
same line is shown in the app's Languages window.
