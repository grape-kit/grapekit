// Hazır şablonlar ve Markdown rehberi (TR / EN). {date} bugünün tarihiyle doldurulur.
export const TEMPLATES = [
  { id: 'readme', icon: 'file', tr: `# Proje Adı

Projenin ne yaptığını bir iki cümleyle anlatın.

## Özellikler

- Birinci özellik
- İkinci özellik
- Üçüncü özellik

## Kurulum

\`\`\`bash
git clone https://github.com/kullanici/proje.git
cd proje
\`\`\`

## Kullanım

\`\`\`bash
proje --yardim
\`\`\`

## Katkıda bulunma

Hata bildirimleri ve öneriler memnuniyetle karşılanır.

## Lisans

[MIT](LICENSE)
`, en: `# Project Name

Describe what the project does in a sentence or two.

## Features

- First feature
- Second feature
- Third feature

## Installation

\`\`\`bash
git clone https://github.com/user/project.git
cd project
\`\`\`

## Usage

\`\`\`bash
project --help
\`\`\`

## Contributing

Bug reports and ideas are welcome.

## License

[MIT](LICENSE)
` },
  { id: 'meeting', icon: 'calendar', tr: `---
title: Toplantı notu
date: {date}
---

# Toplantı: Konu

**Tarih:** {date}
**Katılımcılar:** Ad Soyad, Ad Soyad

## Gündem

1. Birinci madde
2. İkinci madde

## Notlar

-

## Kararlar

- [ ] Karar 1

## Yapılacaklar

| Görev | Sorumlu | Son tarih |
| ----- | ------- | --------- |
|       |         |           |
`, en: `---
title: Meeting notes
date: {date}
---

# Meeting: Topic

**Date:** {date}
**Attendees:** Name Surname, Name Surname

## Agenda

1. First item
2. Second item

## Notes

-

## Decisions

- [ ] Decision 1

## Action items

| Task | Owner | Due |
| ---- | ----- | --- |
|      |       |     |
` },
  { id: 'blog', icon: 'pen', tr: `---
title: Yazı başlığı
author: Ad Soyad
date: {date}
tags: [etiket, etiket]
---

# Yazı başlığı

Okuyucuyu yakalayan kısa bir giriş paragrafı.

## Birinci bölüm

Ana fikir. **Önemli noktaları** vurgulayın.

> Dikkat çekici bir alıntı.

## İkinci bölüm

![Görsel açıklaması](resimler/gorsel.png)

## Sonuç

Kısa bir toparlama ve okuyucuya çağrı.
`, en: `---
title: Post title
author: Name Surname
date: {date}
tags: [tag, tag]
---

# Post title

A short opening paragraph that hooks the reader.

## First section

The main idea. Highlight **key points**.

> An eye-catching quote.

## Second section

![Image description](images/image.png)

## Conclusion

A short wrap-up and a call to action.
` },
  { id: 'cv', icon: 'file', tr: `# Ad Soyad

**Unvan** · Şehir · [e-posta@ornek.com](mailto:e-posta@ornek.com) · [linkedin.com/in/kullanici](https://linkedin.com/in/kullanici)

## Özet

Deneyiminizi ve hedefinizi anlatan iki üç cümle.

## Deneyim

### Pozisyon — Şirket
*Ocak 2023 – Bugün*

- Başarı ya da sorumluluk, mümkünse sayıyla
- Başarı ya da sorumluluk

### Pozisyon — Şirket
*2020 – 2022*

- Başarı ya da sorumluluk

## Eğitim

**Bölüm** — Üniversite (2016 – 2020)

## Yetenekler

| Alan | Yetenekler |
| ---- | ---------- |
| Teknik | … |
| Diller | Türkçe (ana dil), İngilizce (ileri) |
`, en: `# Name Surname

**Title** · City · [email@example.com](mailto:email@example.com) · [linkedin.com/in/user](https://linkedin.com/in/user)

## Summary

Two or three sentences about your experience and goals.

## Experience

### Position — Company
*January 2023 – Present*

- Achievement or responsibility, with numbers if possible
- Achievement or responsibility

### Position — Company
*2020 – 2022*

- Achievement or responsibility

## Education

**Degree** — University (2016 – 2020)

## Skills

| Area | Skills |
| ---- | ------ |
| Technical | … |
| Languages | English (native), … |
` },
  { id: 'plan', icon: 'diagram', tr: `---
title: Proje planı
date: {date}
---

# Proje planı

[TOC]

## Amaç

Projenin neden yapıldığı ve başarı ölçütleri.

## Kapsam

- Dahil:
- Hariç:

## Zaman çizelgesi

\`\`\`mermaid
gantt
  title Proje takvimi
  dateFormat YYYY-MM-DD
  section Hazırlık
  Araştırma :a1, {date}, 7d
  Tasarım   :after a1, 10d
  section Yapım
  Geliştirme :12d
  Test       :5d
\`\`\`

## Riskler

| Risk | Olasılık | Etki | Önlem |
| ---- | -------- | ---- | ----- |
|      |          |      |       |

> [!IMPORTANT]
> Kritik bağımlılıkları buraya yazın.
`, en: `---
title: Project plan
date: {date}
---

# Project plan

[TOC]

## Goal

Why the project exists and how success is measured.

## Scope

- In:
- Out:

## Timeline

\`\`\`mermaid
gantt
  title Project schedule
  dateFormat YYYY-MM-DD
  section Preparation
  Research :a1, {date}, 7d
  Design   :after a1, 10d
  section Build
  Development :12d
  Testing     :5d
\`\`\`

## Risks

| Risk | Likelihood | Impact | Mitigation |
| ---- | ---------- | ------ | ---------- |
|      |            |        |            |

> [!IMPORTANT]
> List critical dependencies here.
` },
  { id: 'weekly', icon: 'clock', tr: `# Haftalık rapor — {date}

## Bu hafta tamamlananlar

- [x] …

## Devam edenler

- [ ] …

## Gelecek hafta

- [ ] …

## Engeller

> [!WARNING]
> Yardım gereken konular.
`, en: `# Weekly report — {date}

## Done this week

- [x] …

## In progress

- [ ] …

## Next week

- [ ] …

## Blockers

> [!WARNING]
> Things that need help.
` },
  { id: 'paper', icon: 'math', tr: `---
title: Ödev başlığı
author: Ad Soyad
date: {date}
---

# Ödev başlığı

## Özet

Çalışmanın kısa özeti.

## Giriş

Konunun arka planı[^1].

## Yöntem

İkinci dereceden denklemin kökleri:

$$
x = \\frac{-b \\pm \\sqrt{b^2 - 4ac}}{2a}
$$

Satır içi formül örneği: $E = mc^2$.

## Bulgular

| Değişken | Ortalama | Std. sapma |
| -------- | -------: | ---------: |
| A        |     12,4 |        1,2 |

## Sonuç

## Kaynaklar

[^1]: Yazar, *Kitap adı*, Yayınevi, Yıl.
`, en: `---
title: Paper title
author: Name Surname
date: {date}
---

# Paper title

## Abstract

A short summary of the work.

## Introduction

Background of the topic[^1].

## Method

Roots of a quadratic equation:

$$
x = \\frac{-b \\pm \\sqrt{b^2 - 4ac}}{2a}
$$

Inline formula example: $E = mc^2$.

## Results

| Variable | Mean | Std. dev. |
| -------- | ---: | --------: |
| A        | 12.4 |       1.2 |

## Conclusion

## References

[^1]: Author, *Book title*, Publisher, Year.
` },
  { id: 'changelog', icon: 'layers', tr: `# Değişiklik günlüğü

Bu projedeki önemli değişiklikler burada listelenir.

## [Yayınlanmadı]

### Eklenenler
-

### Değişenler
-

### Düzeltilenler
-

## [1.0.0] — {date}

### Eklenenler
- İlk sürüm
`, en: `# Changelog

All notable changes to this project are listed here.

## [Unreleased]

### Added
-

### Changed
-

### Fixed
-

## [1.0.0] — {date}

### Added
- First release
` },
];

export const CHEATSHEET = {
  tr: `# Markdown rehberi

Markdown, düz metni birkaç işaretle biçimlendirmenin en kolay yoludur. Sol tarafta yazdığın her şey sağda anında görünür.

## Metin

| Yazılan | Sonuç |
| ------- | ----- |
| \`**kalın**\` | **kalın** |
| \`*italik*\` | *italik* |
| \`~~üstü çizili~~\` | ~~üstü çizili~~ |
| \`==vurgulu==\` | ==vurgulu== |
| \`\\\`kod\\\`\` | \`kod\` |
| \`H~2~O\`, \`x^2^\` | H~2~O, x^2^ |
| \`:rocket:\` | :rocket: |

## Başlıklar

\`\`\`markdown
# Başlık 1
## Başlık 2
### Başlık 3
\`\`\`

## Listeler

- Madde
  - Alt madde
1. Numaralı
2. Liste
- [x] Tamamlanan görev
- [ ] Bekleyen görev

## Bağlantı ve görsel

\`\`\`markdown
[Grapekit](https://grapekit.com)
![Açıklama](resimler/gorsel.png)
\`\`\`

Görselleri doğrudan **yapıştırabilir** ya da pencereye **sürükleyebilirsin**; belgenin yanına kaydedilir.

## Alıntı ve uyarı kutuları

> Sıradan bir alıntı.

> [!TIP]
> \`> [!NOTE]\`, \`[!TIP]\`, \`[!IMPORTANT]\`, \`[!WARNING]\`, \`[!CAUTION]\` kullanabilirsin.

## Kod

\`\`\`javascript
function merhaba(ad) {
  return \`Merhaba, \${ad}!\`;
}
\`\`\`

## Tablo

| Sola | Ortaya | Sağa |
| :--- | :----: | ---: |
| a    |   b    |    c |

İmleç tablodayken **Tab** ile hücreler arasında gezinebilir, **Tablo** menüsünden satır ve sütun ekleyebilirsin.

## Matematik

Satır içi: $a^2 + b^2 = c^2$

$$
\\int_0^\\infty e^{-x^2}\\,dx = \\frac{\\sqrt{\\pi}}{2}
$$

## Diyagram

\`\`\`mermaid
flowchart LR
  A[Yaz] --> B{Hazır mı?}
  B -- Evet --> C[PDF'e aktar]
  B -- Hayır --> A
\`\`\`

## Dipnot

Bir cümle[^not].

[^not]: Dipnot metni.

## Tanım listesi

Markdown
: Düz metin biçimlendirme dili.

## Diğer

- \`---\` yatay çizgi
- \`[TOC]\` içindekiler
- \`<!-- pagebreak -->\` PDF'te sayfa sonu
- Belgenin başına \`---\` ile ön bilgi (başlık, yazar, tarih) yazabilirsin.
`,
  en: `# Markdown guide

Markdown is the easiest way to format plain text with a few symbols. Everything you type on the left appears instantly on the right.

## Text

| You type | You get |
| -------- | ------- |
| \`**bold**\` | **bold** |
| \`*italic*\` | *italic* |
| \`~~strikethrough~~\` | ~~strikethrough~~ |
| \`==highlight==\` | ==highlight== |
| \`\\\`code\\\`\` | \`code\` |
| \`H~2~O\`, \`x^2^\` | H~2~O, x^2^ |
| \`:rocket:\` | :rocket: |

## Headings

\`\`\`markdown
# Heading 1
## Heading 2
### Heading 3
\`\`\`

## Lists

- Item
  - Sub-item
1. Numbered
2. List
- [x] Done task
- [ ] Open task

## Links and images

\`\`\`markdown
[Grapekit](https://grapekit.com)
![Description](images/image.png)
\`\`\`

You can **paste** images or **drag** them into the window; they are saved next to the document.

## Quotes and alerts

> A regular quote.

> [!TIP]
> Use \`> [!NOTE]\`, \`[!TIP]\`, \`[!IMPORTANT]\`, \`[!WARNING]\` or \`[!CAUTION]\`.

## Code

\`\`\`javascript
function hello(name) {
  return \`Hello, \${name}!\`;
}
\`\`\`

## Table

| Left | Center | Right |
| :--- | :----: | ----: |
| a    |   b    |     c |

With the cursor in a table, press **Tab** to move between cells and use the **Table** menu to add rows and columns.

## Math

Inline: $a^2 + b^2 = c^2$

$$
\\int_0^\\infty e^{-x^2}\\,dx = \\frac{\\sqrt{\\pi}}{2}
$$

## Diagram

\`\`\`mermaid
flowchart LR
  A[Write] --> B{Ready?}
  B -- Yes --> C[Export to PDF]
  B -- No --> A
\`\`\`

## Footnote

A sentence[^note].

[^note]: The footnote text.

## Definition list

Markdown
: A plain-text formatting language.

## More

- \`---\` horizontal rule
- \`[TOC]\` table of contents
- \`<!-- pagebreak -->\` page break in PDF
- Start the document with \`---\` front matter (title, author, date).
`,
};

export function templateText(tpl, lang) {
  const d = new Date();
  const date = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  return (lang === 'en' ? tpl.en : tpl.tr).replaceAll('{date}', date);
}
