// Arayüz metinleri: [Türkçe, English]
const S = {
  'app.name': ['Markdown Editörü', 'Markdown Studio'],
  'untitled': ['Adsız', 'Untitled'],
  'win.min': ['Simge durumuna küçült', 'Minimize'], 'win.max': ['Ekranı kapla', 'Maximize'], 'win.restore': ['Önceki boyut', 'Restore'], 'win.close': ['Kapat', 'Close'],

  // Menüler
  'menu.file': ['Dosya', 'File'], 'menu.edit': ['Düzen', 'Edit'], 'menu.view': ['Görünüm', 'View'], 'menu.insert': ['Ekle', 'Insert'],
  'menu.format': ['Biçim', 'Format'], 'menu.table': ['Tablo', 'Table'], 'menu.export': ['Dışa aktar', 'Export'], 'menu.help': ['Yardım', 'Help'],

  // Dosya
  'cmd.file.new': ['Yeni belge', 'New document'], 'cmd.file.newTemplate': ['Şablondan yeni…', 'New from template…'],
  'cmd.file.open': ['Aç…', 'Open…'], 'cmd.file.openFolder': ['Klasör aç…', 'Open folder…'], 'cmd.file.save': ['Kaydet', 'Save'],
  'cmd.file.saveAs': ['Farklı kaydet…', 'Save as…'], 'cmd.file.saveAll': ['Tümünü kaydet', 'Save all'], 'cmd.file.close': ['Sekmeyi kapat', 'Close tab'],
  'cmd.file.reopen': ['Kapatılan sekmeyi aç', 'Reopen closed tab'], 'cmd.file.recent': ['Son dosyalar', 'Recent files'],
  'cmd.file.import': ['İçe aktar (HTML / Word)…', 'Import (HTML / Word)…'], 'cmd.file.reveal': ['Klasörde göster', 'Show in folder'],
  'cmd.file.saveTemplate': ['Şablon olarak kaydet…', 'Save as template…'], 'cmd.file.print': ['Yazdır…', 'Print…'],
  'cmd.file.settings': ['Ayarlar', 'Settings'], 'cmd.file.exit': ['Çıkış', 'Exit'], 'cmd.file.encoding': ['Kodlama', 'Encoding'], 'cmd.file.eol': ['Satır sonu', 'Line ending'],

  // Düzen
  'cmd.edit.undo': ['Geri al', 'Undo'], 'cmd.edit.redo': ['Yinele', 'Redo'], 'cmd.edit.cut': ['Kes', 'Cut'], 'cmd.edit.copy': ['Kopyala', 'Copy'],
  'cmd.edit.paste': ['Yapıştır', 'Paste'], 'cmd.edit.pastePlain': ['Düz metin olarak yapıştır', 'Paste as plain text'], 'cmd.edit.selectAll': ['Tümünü seç', 'Select all'],
  'cmd.edit.find': ['Bul', 'Find'], 'cmd.edit.replace': ['Değiştir', 'Replace'], 'cmd.edit.findInFiles': ['Dosyalarda ara', 'Find in files'],
  'cmd.edit.gotoLine': ['Satıra git…', 'Go to line…'], 'cmd.edit.nextMatch': ['Sonraki eşleşmeyi de seç', 'Add next match'],
  'cmd.edit.lineUp': ['Satırı yukarı taşı', 'Move line up'], 'cmd.edit.lineDown': ['Satırı aşağı taşı', 'Move line down'], 'cmd.edit.dupLine': ['Satırı çoğalt', 'Duplicate line'],
  'cmd.edit.palette': ['Komut paleti…', 'Command palette…'], 'cmd.edit.copyHtml': ['HTML olarak kopyala', 'Copy as HTML'],

  // Görünüm
  'cmd.view.editor': ['Yalnızca düzenleyici', 'Editor only'], 'cmd.view.split': ['Yan yana', 'Side by side'], 'cmd.view.preview': ['Yalnızca önizleme', 'Preview only'],
  'cmd.view.sidebar': ['Kenar çubuğu', 'Sidebar'], 'cmd.view.outline': ['İçindekiler', 'Outline'], 'cmd.view.files': ['Dosyalar', 'Files'], 'cmd.view.search': ['Ara', 'Search'],
  'cmd.view.focus': ['Odak modu', 'Focus mode'], 'cmd.view.typewriter': ['Daktilo modu', 'Typewriter mode'], 'cmd.view.zen': ['Dikkat dağıtmayan mod', 'Distraction-free mode'],
  'cmd.view.fullscreen': ['Tam ekran', 'Full screen'], 'cmd.view.syncScroll': ['Senkron kaydırma', 'Sync scrolling'], 'cmd.view.wrap': ['Satır kaydırma', 'Word wrap'],
  'cmd.view.lineNumbers': ['Satır numaraları', 'Line numbers'], 'cmd.view.zoomIn': ['Yakınlaştır', 'Zoom in'], 'cmd.view.zoomOut': ['Uzaklaştır', 'Zoom out'], 'cmd.view.zoomReset': ['Yakınlaştırmayı sıfırla', 'Reset zoom'],
  'cmd.view.theme': ['Önizleme teması', 'Preview theme'], 'cmd.view.appTheme': ['Uygulama teması', 'App theme'], 'cmd.view.spell': ['Yazım denetimi', 'Spell check'],

  // Ekle
  'cmd.insert.link': ['Bağlantı…', 'Link…'], 'cmd.insert.image': ['Görsel…', 'Image…'], 'cmd.insert.table': ['Tablo…', 'Table…'],
  'cmd.insert.code': ['Kod bloğu', 'Code block'], 'cmd.insert.hr': ['Yatay çizgi', 'Horizontal rule'], 'cmd.insert.math': ['Matematik formülü', 'Math formula'],
  'cmd.insert.mathInline': ['Satır içi formül', 'Inline formula'], 'cmd.insert.mermaid': ['Diyagram (Mermaid)', 'Diagram (Mermaid)'],
  'cmd.insert.alert': ['Uyarı kutusu', 'Alert box'], 'cmd.insert.footnote': ['Dipnot', 'Footnote'], 'cmd.insert.pagebreak': ['Sayfa sonu', 'Page break'],
  'cmd.insert.toc': ['İçindekiler', 'Table of contents'], 'cmd.insert.frontMatter': ['Ön bilgi (başlık, yazar, tarih)', 'Front matter (title, author, date)'],
  'cmd.insert.date': ['Tarih ve saat', 'Date and time'], 'cmd.insert.emoji': ['Emoji…', 'Emoji…'], 'cmd.insert.details': ['Açılır bölüm', 'Collapsible section'],

  // Biçim
  'cmd.fmt.bold': ['Kalın', 'Bold'], 'cmd.fmt.italic': ['İtalik', 'Italic'], 'cmd.fmt.strike': ['Üstü çizili', 'Strikethrough'], 'cmd.fmt.code': ['Satır içi kod', 'Inline code'],
  'cmd.fmt.mark': ['Vurgula', 'Highlight'], 'cmd.fmt.sup': ['Üst simge', 'Superscript'], 'cmd.fmt.sub': ['Alt simge', 'Subscript'],
  'cmd.fmt.h': ['Başlık', 'Heading'], 'cmd.fmt.h0': ['Normal metin', 'Normal text'], 'cmd.fmt.quote': ['Alıntı', 'Quote'],
  'cmd.fmt.ul': ['Madde işaretli liste', 'Bulleted list'], 'cmd.fmt.ol': ['Numaralı liste', 'Numbered list'], 'cmd.fmt.task': ['Görev listesi', 'Task list'],
  'cmd.fmt.indent': ['Girintiyi artır', 'Indent'], 'cmd.fmt.outdent': ['Girintiyi azalt', 'Outdent'], 'cmd.fmt.clear': ['Biçimi temizle', 'Clear formatting'],

  // Tablo
  'cmd.tbl.format': ['Tabloyu hizala', 'Format table'], 'cmd.tbl.rowAbove': ['Üste satır ekle', 'Insert row above'], 'cmd.tbl.rowBelow': ['Alta satır ekle', 'Insert row below'],
  'cmd.tbl.colLeft': ['Sola sütun ekle', 'Insert column left'], 'cmd.tbl.colRight': ['Sağa sütun ekle', 'Insert column right'],
  'cmd.tbl.delRow': ['Satırı sil', 'Delete row'], 'cmd.tbl.delCol': ['Sütunu sil', 'Delete column'],
  'cmd.tbl.alignL': ['Sola hizala', 'Align left'], 'cmd.tbl.alignC': ['Ortala', 'Align center'], 'cmd.tbl.alignR': ['Sağa hizala', 'Align right'],

  // Dışa aktar
  'cmd.exp.pdf': ['PDF…', 'PDF…'], 'cmd.exp.html': ['HTML…', 'HTML…'], 'cmd.exp.docx': ['Word (.docx)…', 'Word (.docx)…'], 'cmd.exp.png': ['Görsel (PNG)…', 'Image (PNG)…'],
  'cmd.exp.batch': ['Toplu dönüştür…', 'Batch convert…'],

  // Yardım
  'cmd.help.cheatsheet': ['Markdown rehberi', 'Markdown guide'], 'cmd.help.shortcuts': ['Klavye kısayolları', 'Keyboard shortcuts'],
  'cmd.help.feedback': ['Hata bildir / öneri', 'Report a bug / idea'], 'cmd.help.updates': ['Güncellemeleri denetle', 'Check for updates'],
  'cmd.help.about': ['Hakkında', 'About'], 'cmd.help.site': ['Uygulama sayfası', 'App page'], 'cmd.help.coffee': ['Bize kahve ısmarla', 'Buy us a coffee'],

  // Kenar çubuğu
  'side.outline': ['İçindekiler', 'Outline'], 'side.files': ['Dosyalar', 'Files'], 'side.search': ['Ara', 'Search'],
  'side.noOutline': ['Belgede başlık yok. "# Başlık" yazarak ekleyin.', 'No headings yet. Type "# Heading" to add one.'],
  'side.noFolder': ['Bir klasör açın; içindeki Markdown dosyaları burada listelenir.', 'Open a folder to list its Markdown files here.'],
  'side.openFolder': ['Klasör aç', 'Open folder'], 'side.newFile': ['Yeni dosya', 'New file'], 'side.newFolder': ['Yeni klasör', 'New folder'],
  'side.rename': ['Yeniden adlandır', 'Rename'], 'side.delete': ['Sil', 'Delete'], 'side.refresh': ['Yenile', 'Refresh'], 'side.closeFolder': ['Klasörü kapat', 'Close folder'],
  'side.searchPh': ['Klasörde ara…', 'Search in folder…'], 'side.searchRegex': ['Düzenli ifade', 'Regular expression'], 'side.searchCase': ['Büyük/küçük harf', 'Match case'],
  'side.noResults': ['Sonuç yok', 'No results'], 'side.results': ['{n} sonuç, {f} dosyada', '{n} results in {f} files'], 'side.needFolder': ['Aramak için önce bir klasör açın.', 'Open a folder to search.'],
  'side.deleteConfirm': ['"{name}" Geri Dönüşüm Kutusu\'na taşınsın mı?', 'Move "{name}" to the Recycle Bin?'],

  // Durum çubuğu
  'st.line': ['Satır {l}, Sütun {c}', 'Ln {l}, Col {c}'], 'st.sel': ['{n} seçili', '{n} selected'], 'st.words': ['{n} kelime', '{n} words'],
  'st.chars': ['{n} karakter', '{n} characters'], 'st.read': ['{n} dk okuma', '{n} min read'], 'st.saved': ['Kaydedildi', 'Saved'], 'st.unsaved': ['Kaydedilmedi', 'Unsaved'],
  'st.autosave': ['Otomatik kaydetme', 'Auto save'],

  // Karşılama
  'welcome.title': ['Hoş geldin', 'Welcome'], 'welcome.lead': ['Yazmaya başla, aç ya da bir şablon seç.', 'Start writing, open a file or pick a template.'],
  'welcome.new': ['Boş belge', 'Blank document'], 'welcome.open': ['Dosya aç', 'Open file'], 'welcome.folder': ['Klasör aç', 'Open folder'],
  'welcome.templates': ['Şablonlar', 'Templates'], 'welcome.recent': ['Son dosyalar', 'Recent files'], 'welcome.none': ['Henüz yok', 'Nothing yet'],
  'editor.placeholder': ['Yazmaya başla… Markdown biçimlendirme için araç çubuğunu ya da Ctrl+P komut paletini kullanabilirsin.', 'Start writing… Use the toolbar or the Ctrl+P command palette for Markdown formatting.'],

  // Genel
  'ok': ['Tamam', 'OK'], 'cancel': ['Vazgeç', 'Cancel'], 'save': ['Kaydet', 'Save'], 'dontSave': ['Kaydetme', "Don't save"], 'close': ['Kapat', 'Close'],
  'insert': ['Ekle', 'Insert'], 'apply': ['Uygula', 'Apply'], 'browse': ['Gözat…', 'Browse…'], 'yes': ['Evet', 'Yes'], 'no': ['Hayır', 'No'], 'copy': ['Kopyala', 'Copy'], 'copied': ['Kopyalandı', 'Copied'],
  'export': ['Dışa aktar', 'Export'], 'start': ['Başlat', 'Start'], 'reset': ['Sıfırla', 'Reset'], 'open': ['Aç', 'Open'], 'restore': ['Geri yükle', 'Restore'], 'discard': ['At', 'Discard'],
  'on': ['Açık', 'On'], 'off': ['Kapalı', 'Off'], 'system': ['Sistem', 'System'], 'light': ['Açık', 'Light'], 'dark': ['Koyu', 'Dark'],

  // Sorular / uyarılar
  'ask.saveChanges': ['"{name}" belgesindeki değişiklikler kaydedilsin mi?', 'Save changes to "{name}"?'],
  'ask.saveChangesMany': ['{n} belgede kaydedilmemiş değişiklik var. Kaydedilsin mi?', '{n} documents have unsaved changes. Save them?'],
  'ask.changedOnDisk': ['"{name}" başka bir programda değiştirildi. Diskteki sürüm yüklensin mi? Senin değişikliklerin kaybolur.', '"{name}" was changed by another program. Load the version on disk? Your changes will be lost.'],
  'ask.keepMine': ['Benimkini tut', 'Keep mine'], 'ask.loadDisk': ['Diskteki sürümü yükle', 'Load from disk'],
  'ask.deletedOnDisk': ['"{name}" diskte silinmiş ya da taşınmış. Belge açık kalıyor; kaydedersen yeniden oluşturulur.', '"{name}" was deleted or moved. The document stays open; saving recreates it.'],
  'ask.recovery': ['Beklenmedik bir kapanıştan önce kaydedilmemiş {n} belge bulundu. Geri yüklensin mi?', '{n} unsaved documents from an unexpected shutdown were found. Restore them?'],
  'ask.saveFirstImage': ['Görselleri belgenin yanına kaydedebilmek için önce belgeyi kaydet.', 'Save the document first so images can be stored next to it.'],
  'err.open': ['Dosya açılamadı: {m}', "Couldn't open the file: {m}"], 'err.save': ['Kaydedilemedi: {m}', "Couldn't save: {m}"],
  'err.export': ['Dışa aktarılamadı: {m}', "Couldn't export: {m}"], 'err.import': ['İçe aktarılamadı: {m}', "Couldn't import: {m}"],
  'err.tooBig': ['Bu dosya çok büyük (50 MB üstü).', 'This file is too large (over 50 MB).'],
  'info.exported': ['Dışa aktarıldı: {name}', 'Exported: {name}'], 'info.openFile': ['Dosyayı aç', 'Open file'], 'info.openFolder': ['Klasörü aç', 'Open folder'],
  'info.imageSaved': ['Görsel kaydedildi: {name}', 'Image saved: {name}'], 'info.copiedHtml': ['HTML panoya kopyalandı', 'HTML copied to clipboard'],
  'info.templateSaved': ['Şablon kaydedildi', 'Template saved'],

  // Diyaloglar
  'dlg.link.title': ['Bağlantı ekle', 'Insert link'], 'dlg.link.text': ['Metin', 'Text'], 'dlg.link.url': ['Adres', 'URL'],
  'dlg.image.title': ['Görsel ekle', 'Insert image'], 'dlg.image.alt': ['Açıklama (alt metin)', 'Description (alt text)'], 'dlg.image.src': ['Dosya ya da adres', 'File or URL'],
  'dlg.image.copy': ['Görseli belgenin klasörüne kopyala', "Copy the image into the document's folder"],
  'dlg.table.title': ['Tablo ekle', 'Insert table'], 'dlg.table.rows': ['Satır', 'Rows'], 'dlg.table.cols': ['Sütun', 'Columns'], 'dlg.table.hint': ['Kareleri seçerek boyutu belirle', 'Pick a size from the grid'],
  'dlg.table.header': ['Başlık', 'Header'],
  'dlg.goto.title': ['Satıra git', 'Go to line'], 'dlg.goto.ph': ['Satır numarası (1–{n})', 'Line number (1–{n})'],
  'dlg.palette.ph': ['Komut ara…', 'Search commands…'],
  'dlg.tpl.title': ['Şablondan yeni belge', 'New from template'], 'dlg.tpl.mine': ['Şablonlarım', 'My templates'], 'dlg.tpl.name': ['Şablon adı', 'Template name'],
  'dlg.emoji.title': ['Emoji ekle', 'Insert emoji'], 'dlg.emoji.ph': ['Emoji ara (ör. rocket, smile)…', 'Search emoji (e.g. rocket, smile)…'],
  'dlg.alert.title': ['Uyarı kutusu', 'Alert box'],

  'alert.note': ['Not', 'Note'], 'alert.tip': ['İpucu', 'Tip'], 'alert.important': ['Önemli', 'Important'], 'alert.warning': ['Uyarı', 'Warning'], 'alert.caution': ['Dikkat', 'Caution'],

  // Dışa aktarma diyalogları
  'pdf.title': ['PDF olarak dışa aktar', 'Export as PDF'], 'pdf.paper': ['Kâğıt', 'Paper'], 'pdf.orient': ['Yön', 'Orientation'],
  'pdf.portrait': ['Dikey', 'Portrait'], 'pdf.landscape': ['Yatay', 'Landscape'], 'pdf.margins': ['Kenar boşlukları', 'Margins'],
  'pdf.mNarrow': ['Dar', 'Narrow'], 'pdf.mNormal': ['Normal', 'Normal'], 'pdf.mWide': ['Geniş', 'Wide'], 'pdf.scale': ['Ölçek', 'Scale'],
  'pdf.theme': ['Tema', 'Theme'], 'pdf.header': ['Üst bilgi', 'Header'], 'pdf.footer': ['Alt bilgi', 'Footer'],
  'pdf.hNone': ['Yok', 'None'], 'pdf.hTitle': ['Belge başlığı', 'Document title'], 'pdf.hTitleDate': ['Başlık ve tarih', 'Title and date'],
  'pdf.fPage': ['Sayfa numarası', 'Page number'], 'pdf.fPageOf': ['Sayfa X / Y', 'Page X of Y'],
  'pdf.cover': ['Kapak sayfası', 'Cover page'], 'pdf.coverHint': ['Ön bilgideki başlık, yazar ve tarihten', 'From the title, author and date in the front matter'],
  'pdf.toc': ['İçindekiler sayfası', 'Table of contents page'], 'pdf.background': ['Arka plan renklerini yazdır', 'Print background colors'],
  'pdf.breakH1': ['Her ana başlıkta yeni sayfa', 'New page for every top-level heading'], 'pdf.preview': ['Önizleme', 'Preview'],
  'pdf.pageOf': ['Sayfa {p} / {t}', 'Page {p} of {t}'], 'pdf.page': ['{p}', '{p}'], 'pdf.contents': ['İçindekiler', 'Contents'],
  'html.title': ['HTML olarak dışa aktar', 'Export as HTML'], 'html.embed': ['Görselleri dosyaya göm', 'Embed images in the file'],
  'docx.title': ['Word olarak dışa aktar', 'Export as Word'], 'png.title': ['Görsel olarak dışa aktar', 'Export as image'],
  'png.scale': ['Çözünürlük', 'Resolution'], 'png.width': ['Genişlik', 'Width'],
  'batch.title': ['Toplu dönüştür', 'Batch convert'], 'batch.folder': ['Kaynak klasör', 'Source folder'], 'batch.sub': ['Alt klasörleri de dahil et', 'Include subfolders'],
  'batch.format': ['Hedef biçim', 'Target format'], 'batch.out': ['Çıktı klasörü', 'Output folder'], 'batch.same': ['Kaynak dosyaların yanına', 'Next to the source files'],
  'batch.found': ['{n} Markdown dosyası bulundu', '{n} Markdown files found'], 'batch.done': ['{ok} dosya dönüştürüldü, {fail} hata', '{ok} files converted, {fail} errors'],
  'batch.running': ['Dönüştürülüyor: {i} / {n}', 'Converting: {i} / {n}'],
  'import.title': ['İçe aktar', 'Import'],

  // Ayarlar
  'set.title': ['Ayarlar', 'Settings'], 'set.general': ['Genel', 'General'], 'set.editor': ['Düzenleyici', 'Editor'], 'set.preview': ['Önizleme', 'Preview'],
  'set.export': ['Dışa aktarma', 'Export'], 'set.files': ['Dosyalar', 'Files'], 'set.shortcuts': ['Kısayollar', 'Shortcuts'], 'set.about': ['Hakkında', 'About'],
  'set.lang': ['Dil', 'Language'], 'set.theme': ['Uygulama teması', 'App theme'], 'set.startup': ['Açılışta', 'On startup'],
  'set.restoreSession': ['Son oturumu geri yükle', 'Restore the last session'], 'set.restoreSessionD': ['Açık sekmeler ve imleç konumları geri gelir', 'Reopens your tabs and cursor positions'],
  'set.font': ['Yazı tipi', 'Font'], 'set.fontSize': ['Yazı boyutu', 'Font size'], 'set.tabSize': ['Sekme genişliği', 'Tab width'],
  'set.wrap': ['Satır kaydırma', 'Word wrap'], 'set.lineNumbers': ['Satır numaraları', 'Line numbers'], 'set.spell': ['Yazım denetimi', 'Spell check'],
  'set.spellLang': ['Yazım denetimi dili', 'Spell check language'], 'set.maxWidth': ['Düzenleyici satır genişliği', 'Editor line width'], 'set.unlimited': ['Sınırsız', 'Unlimited'],
  'set.smartPaste': ['Biçimli yapıştırmayı Markdown\'a çevir', 'Convert rich paste to Markdown'], 'set.smartPasteD': ['Web ve Word\'den kopyalanan içerik Markdown olarak yapıştırılır', 'Content copied from the web or Word is pasted as Markdown'],
  'set.previewTheme': ['Önizleme teması', 'Preview theme'], 'set.previewFont': ['Önizleme yazı boyutu', 'Preview font size'], 'set.previewWidth': ['Önizleme satır genişliği', 'Preview line width'],
  'set.lineHeight': ['Satır aralığı', 'Line height'], 'set.breaks': ['Tek satır sonlarını koru', 'Keep single line breaks'], 'set.breaksD': ['Enter ile alt satıra geçmek önizlemede de alt satır olur', 'A single Enter becomes a line break in the preview'],
  'set.remoteImages': ['İnternetten görselleri yükle', 'Load images from the internet'], 'set.remoteImagesD': ['Kapalıyken yalnızca bilgisayardaki görseller gösterilir', 'When off, only local images are shown'],
  'set.syncScroll': ['Senkron kaydırma', 'Sync scrolling'], 'set.codeLines': ['Kod bloklarında satır numarası', 'Line numbers in code blocks'],
  'set.autosave': ['Otomatik kaydet', 'Auto save'], 'set.autosaveD': ['Kaydedilmiş belgeler yazmayı bıraktıktan sonra kendiliğinden kaydedilir', 'Saved documents are saved automatically after you stop typing'],
  'set.imageFolder': ['Görsel klasörü', 'Image folder'], 'set.imageFolderD': ['Yapıştırılan görseller belgenin yanındaki bu klasöre kaydedilir', 'Pasted images are saved in this folder next to the document'],
  'set.defaultEncoding': ['Yeni dosyalarda kodlama', 'Encoding for new files'], 'set.defaultEol': ['Yeni dosyalarda satır sonu', 'Line ending for new files'],
  'set.associate': ['.md dosyalarını Markdown Editörü ile aç', 'Open .md files with Markdown Studio'], 'set.associateD': ['Windows varsayılan uygulama ayarlarını açar', 'Opens Windows default apps settings'],
  'set.explorer': ['Gezgin\'e "PDF\'e dönüştür" ekle', 'Add "Convert to PDF" to Explorer'], 'set.explorerD': ['Sağ tık menüsünde .md dosyaları için', 'In the right-click menu for .md files'],
  'set.updates': ['Güncellemeleri kendiliğinden denetle', 'Check for updates automatically'], 'set.reset': ['Ayarları sıfırla', 'Reset settings'],
  'set.version': ['Sürüm {v}', 'Version {v}'], 'set.free': ['Ücretsiz · Reklamsız · Açık kaynak (MIT)', 'Free · No ads · Open source (MIT)'],
  'set.privacy': ['Hiçbir veri toplanmaz. Uygulama tamamen çevrimdışı çalışır; yalnızca güncelleme denetimi sürüm numarasını okur.', 'No data is collected. The app works fully offline; only the update check reads a version number.'],
  'set.storeUpdates': ['Güncellemeler Microsoft Store üzerinden gelir.', 'Updates come through the Microsoft Store.'],

  // Şablon adları
  'tpl.readme': ['README', 'README'], 'tpl.meeting': ['Toplantı notu', 'Meeting notes'], 'tpl.blog': ['Blog yazısı', 'Blog post'], 'tpl.cv': ['Özgeçmiş (CV)', 'Résumé (CV)'],
  'tpl.plan': ['Proje planı', 'Project plan'], 'tpl.weekly': ['Haftalık rapor', 'Weekly report'], 'tpl.paper': ['Ödev / makale', 'Assignment / paper'], 'tpl.changelog': ['Değişiklik günlüğü', 'Changelog'],
};

let lang = 'tr';
export function setLang(l) { lang = l === 'en' ? 'en' : 'tr'; document.documentElement.lang = lang; }
export function getLang() { return lang; }

/** Metin; {ad} yer tutucuları vars ile doldurulur. */
export function t(key, vars) {
  const v = S[key];
  let s = v ? v[lang === 'en' ? 1 : 0] : key;
  if (vars) for (const k in vars) s = s.replaceAll('{' + k + '}', vars[k]);
  return s;
}

/** data-i18n / data-i18n-title / data-i18n-ph özniteliklerini doldurur. */
export function applyI18n(root = document) {
  for (const el of root.querySelectorAll('[data-i18n]')) el.textContent = t(el.dataset.i18n);
  for (const el of root.querySelectorAll('[data-i18n-title]')) { el.title = t(el.dataset.i18nTitle); el.setAttribute('aria-label', el.title); }
  for (const el of root.querySelectorAll('[data-i18n-ph]')) el.placeholder = t(el.dataset.i18nPh);
}
