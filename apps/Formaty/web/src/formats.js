// Hedef biçimler ve işlemler: hangi dosya türünü kabul eder, hangi ayar grubunu kullanır, iş isteğine nasıl çevrilir.
// Gruplar: image | video | audio | pdf. Bir hedef yalnızca "accepts" listesindeki türlere uygulanır; diğerleri atlanır.

export const GROUPS = ['image', 'video', 'audio', 'pdf'];

// label: düğmede görünen kısa ad; sub: altında küçük açıklama (çeviri anahtarı ya da düz metin)
export const TARGETS = [
  // Fotoğraf
  { id: 'jpg', group: 'image', label: 'JPG', sub: 'fmt.jpg', accepts: ['image'] },
  { id: 'png', group: 'image', label: 'PNG', sub: 'fmt.png', accepts: ['image'] },
  { id: 'webp', group: 'image', label: 'WebP', sub: 'fmt.webp', accepts: ['image'] },
  { id: 'avif', group: 'image', label: 'AVIF', sub: 'fmt.avif', accepts: ['image'] },
  { id: 'jxl', group: 'image', label: 'JPEG XL', sub: 'fmt.jxl', accepts: ['image'] },
  { id: 'gif', group: 'image', label: 'GIF', sub: 'fmt.gif', accepts: ['image'] },
  { id: 'tiff', group: 'image', label: 'TIFF', sub: 'fmt.tiff', accepts: ['image'] },
  { id: 'bmp', group: 'image', label: 'BMP', sub: 'fmt.bmp', accepts: ['image'] },
  { id: 'ico', group: 'image', label: 'ICO', sub: 'fmt.ico', accepts: ['image'] },
  { id: 'jp2', group: 'image', label: 'JPEG 2000', sub: 'fmt.jp2', accepts: ['image'] },
  { id: 'img-pdf', group: 'image', label: 'PDF', sub: 'fmt.imgpdf', accepts: ['image'] },
  // Video
  { id: 'mp4', group: 'video', label: 'MP4', sub: 'fmt.mp4', accepts: ['video'] },
  { id: 'webm', group: 'video', label: 'WebM', sub: 'fmt.webm', accepts: ['video'] },
  { id: 'mkv', group: 'video', label: 'MKV', sub: 'fmt.mkv', accepts: ['video'] },
  { id: 'mov', group: 'video', label: 'MOV', sub: 'fmt.mov', accepts: ['video'] },
  { id: 'avi', group: 'video', label: 'AVI', sub: 'fmt.avi', accepts: ['video'] },
  { id: 'vid-gif', group: 'video', label: 'GIF', sub: 'fmt.vidgif', accepts: ['video', 'image'] },
  { id: 'vid-webp', group: 'video', label: 'WebP', sub: 'fmt.vidwebp', accepts: ['video', 'image'] },
  { id: 'frames', group: 'video', label: 'fmt.framesL', sub: 'fmt.frames', accepts: ['video'] },
  // Ses (videodan ses çıkarma dahil)
  { id: 'mp3', group: 'audio', label: 'MP3', sub: 'fmt.mp3', accepts: ['audio', 'video'] },
  { id: 'aac', group: 'audio', label: 'M4A', sub: 'fmt.aac', accepts: ['audio', 'video'] },
  { id: 'wav', group: 'audio', label: 'WAV', sub: 'fmt.wav', accepts: ['audio', 'video'] },
  { id: 'flac', group: 'audio', label: 'FLAC', sub: 'fmt.flac', accepts: ['audio', 'video'] },
  { id: 'opus', group: 'audio', label: 'Opus', sub: 'fmt.opus', accepts: ['audio', 'video'] },
  { id: 'ogg', group: 'audio', label: 'OGG', sub: 'fmt.ogg', accepts: ['audio', 'video'] },
  { id: 'aiff', group: 'audio', label: 'AIFF', sub: 'fmt.aiff', accepts: ['audio', 'video'] },
  { id: 'alac', group: 'audio', label: 'ALAC', sub: 'fmt.alac', accepts: ['audio', 'video'] },
  // PDF işlemleri
  { id: 'pdf-img', group: 'pdf', label: 'pdf.toImg', sub: 'pdf.toImgD', accepts: ['pdf'], icon: 'image' },
  { id: 'pdf-merge', group: 'pdf', label: 'pdf.merge', sub: 'pdf.mergeD', accepts: ['pdf'], icon: 'merge' },
  { id: 'pdf-split', group: 'pdf', label: 'pdf.split', sub: 'pdf.splitD', accepts: ['pdf'], icon: 'scissors' },
  { id: 'pdf-compress', group: 'pdf', label: 'pdf.compress', sub: 'pdf.compressD', accepts: ['pdf'], icon: 'shrink' },
  { id: 'pdf-protect', group: 'pdf', label: 'pdf.protect', sub: 'pdf.protectD', accepts: ['pdf'], icon: 'lock' },
  { id: 'pdf-unlock', group: 'pdf', label: 'pdf.unlock', sub: 'pdf.unlockD', accepts: ['pdf'], icon: 'unlock' },
];

export const target = (id) => TARGETS.find((x) => x.id === id);

/** Listede bulunan türlere göre gösterilecek gruplar. Video varsa ses grubu da (ses çıkarma). */
export function groupsFor(kinds) {
  const g = new Set();
  if (kinds.has('image')) g.add('image');
  if (kinds.has('video')) { g.add('video'); g.add('audio'); }
  if (kinds.has('audio')) g.add('audio');
  if (kinds.has('pdf')) g.add('pdf');
  // Hareketli GIF/WebP fotoğrafları videoya da çevrilebilir
  return GROUPS.filter((x) => g.has(x));
}

// Gruba göre varsayılan ayarlar (kullanıcının son seçimi bunların üzerine yazılır)
export const DEFAULT_OPTS = {
  image: {
    quality: 82, targetKb: 0, resize: { mode: 'none', width: 1920, height: 1080, long: 1920, percent: 50, upscale: false },
    keepMeta: false, autoRotate: true, background: '#FFFFFF', rotate: '', lossless: false, palette: false, progressive: true,
    keepAnimation: true, watermark: { text: '', position: 'br', size: 4, opacity: 60 }, combinePdf: true, page: 'a4', margin: 10,
  },
  video: {
    quality: 'balanced', vcodec: 'auto', height: 0, fps: 0, targetMb: 0, audio: 'keep', start: 0, end: 0, speed: 1, hw: true, rotate: '',
    width: 480, loop: true, every: 1, imageFormat: 'jpg', combine: false,
  },
  audio: {
    quality: 'high', mode: 'cbr', kbps: 0, vbr: 2, sampleRate: 0, channels: 'keep', bits: 16, compression: 5, normalize: 0, volumeDb: 0,
    trimSilence: false, start: 0, end: 0, speed: 1, combine: false, keepCover: true,
  },
  pdf: {
    imageFormat: 'jpg', dpi: 150, pages: '', quality: 88, mode: 'each', every: 1, ranges: '', level: 'balanced', name: '',
    userPassword: '', password: '',
  },
};

/** Seçilen hedef + ayarlar → kabuğa gidecek iş isteği (op, format, options). */
export function buildRequest(t, o) {
  const pick = (keys) => Object.fromEntries(keys.filter((k) => o[k] !== undefined).map((k) => [k, o[k]]));
  switch (t.group) {
    case 'image':
      if (t.id === 'img-pdf') return { op: o.combinePdf ? 'imagesToPdf' : 'convert', format: 'pdf', options: pick(['page', 'margin', 'quality']), combine: !!o.combinePdf };
      return {
        op: 'convert', format: t.id,
        options: { ...pick(['quality', 'targetKb', 'resize', 'keepMeta', 'autoRotate', 'background', 'rotate', 'lossless', 'palette', 'progressive', 'keepAnimation']), watermark: o.watermark?.text ? o.watermark : undefined },
      };
    case 'video': {
      if (t.id === 'frames') return { op: 'frames', format: o.imageFormat, options: pick(['every', 'imageFormat']) };
      const fmt = t.id === 'vid-gif' ? 'gif' : t.id === 'vid-webp' ? 'webp-anim' : t.id;
      const anim = fmt === 'gif' || fmt === 'webp-anim';
      const opts = anim ? pick(['quality', 'width', 'fps', 'loop', 'start', 'end', 'speed']) : pick(['quality', 'vcodec', 'height', 'fps', 'targetMb', 'audio', 'start', 'end', 'speed', 'hw', 'rotate']);
      if (!anim && o.combine) return { op: 'concat', format: fmt, options: opts, combine: true };
      return { op: 'convert', format: fmt, options: opts };
    }
    case 'audio': {
      const opts = pick(['quality', 'mode', 'kbps', 'vbr', 'sampleRate', 'channels', 'bits', 'compression', 'normalize', 'volumeDb', 'trimSilence', 'start', 'end', 'speed', 'keepCover']);
      if (o.combine) return { op: 'concat', format: t.id, options: opts, combine: true };
      return { op: 'convert', format: t.id, options: opts };
    }
    case 'pdf':
      switch (t.id) {
        case 'pdf-img': return { op: 'pdfToImages', format: o.imageFormat, options: { ...pick(['dpi', 'pages', 'password']), quality: o.quality } };
        case 'pdf-merge': return { op: 'merge', format: 'pdf', options: { name: o.name, passwords: {} }, combine: true };
        case 'pdf-split': return { op: 'split', format: 'pdf', options: pick(['mode', 'every', 'ranges', 'password']) };
        case 'pdf-compress': return { op: 'compress', format: 'pdf', options: pick(['level', 'password']) };
        case 'pdf-protect': return { op: 'protect', format: 'pdf', options: { userPassword: o.userPassword, ownerPassword: '', password: o.password } };
        case 'pdf-unlock': return { op: 'unlock', format: 'pdf', options: { password: o.password } };
      }
  }
  return null;
}

/** Hedef çıktı uzantısı (bilgi amaçlı). */
export function outExt(t, o) {
  if (t.group === 'pdf') return t.id === 'pdf-img' ? o.imageFormat : 'pdf';
  if (t.id === 'img-pdf') return 'pdf';
  if (t.id === 'frames') return o.imageFormat;
  return { 'vid-gif': 'gif', 'vid-webp': 'webp', aac: 'm4a', alac: 'm4a', tiff: 'tif' }[t.id] || t.id;
}

// Gezgin sağ tık hızlı işlemleri → hedef + ayar değişiklikleri
export const QUICK = {
  'img:jpg': ['jpg', { quality: 88 }],
  'img:png': ['png', {}],
  'img:webp': ['webp', { quality: 80 }],
  'img:avif': ['avif', { quality: 60 }],
  'img:pdf': ['img-pdf', { combinePdf: true }],
  'img:small': ['jpg', { quality: 78, resize: { mode: 'long', long: 1920 } }, { template: '{name}_kucuk' }],
  'vid:mp4': ['mp4', { quality: 'balanced', targetMb: 0 }],
  'vid:mp3': ['mp3', { quality: 'high', mode: 'cbr', kbps: 0 }],
  'vid:compress': ['mp4', { quality: 'small', height: 720, targetMb: 0 }, { template: '{name}_kucuk' }],
  'vid:gif': ['vid-gif', { width: 480, fps: 12 }],
  'aud:mp3': ['mp3', { quality: 'high', mode: 'cbr', kbps: 0 }],
  'aud:wav': ['wav', { bits: 16 }],
  'aud:flac': ['flac', {}],
  'aud:aac': ['aac', { quality: 'high' }],
  'pdf:jpg': ['pdf-img', { imageFormat: 'jpg', dpi: 150 }],
  'pdf:compress': ['pdf-compress', { level: 'balanced' }],
  'pdf:merge': ['pdf-merge', {}],
};

// Ana sayfadaki hızlı araçlar: kart → (dosya türü filtresi, hedef, ayar değişiklikleri)
export const TOOLS = [
  { id: 'img-convert', icon: 'image', kind: 'image', target: 'webp', title: 'tool.imgConvert', sub: 'tool.imgConvertD' },
  { id: 'img-small', icon: 'shrink', kind: 'image', target: 'jpg', patch: { quality: 78, resize: { mode: 'long', long: 1920 } }, title: 'tool.imgSmall', sub: 'tool.imgSmallD' },
  { id: 'vid-convert', icon: 'video', kind: 'video', target: 'mp4', title: 'tool.vidConvert', sub: 'tool.vidConvertD' },
  { id: 'vid-compress', icon: 'zap', kind: 'video', target: 'mp4', patch: { targetMb: 25, quality: 'balanced' }, title: 'tool.vidCompress', sub: 'tool.vidCompressD' },
  { id: 'vid-audio', icon: 'audio', kind: 'video', target: 'mp3', title: 'tool.vidAudio', sub: 'tool.vidAudioD' },
  { id: 'vid-gif', icon: 'film', kind: 'video', target: 'vid-gif', title: 'tool.vidGif', sub: 'tool.vidGifD' },
  { id: 'aud-convert', icon: 'wave', kind: 'audio', target: 'mp3', title: 'tool.audConvert', sub: 'tool.audConvertD' },
  { id: 'pdf-merge', icon: 'merge', kind: 'pdf', target: 'pdf-merge', title: 'tool.pdfMerge', sub: 'tool.pdfMergeD' },
  { id: 'img-pdf', icon: 'pdf', kind: 'image', target: 'img-pdf', patch: { combinePdf: true }, title: 'tool.imgPdf', sub: 'tool.imgPdfD' },
  { id: 'pdf-img', icon: 'frames', kind: 'pdf', target: 'pdf-img', title: 'tool.pdfImg', sub: 'tool.pdfImgD' },
  { id: 'pdf-compress', icon: 'shrink', kind: 'pdf', target: 'pdf-compress', title: 'tool.pdfCompress', sub: 'tool.pdfCompressD' },
  { id: 'pdf-split', icon: 'scissors', kind: 'pdf', target: 'pdf-split', title: 'tool.pdfSplit', sub: 'tool.pdfSplitD' },
];

// Yerleşik hazır ayarlar: grup varsayılanlarının üzerine uygulanır (önceki seçimler karışmasın)
export const BUILTIN_PRESETS = [
  { id: 'web-webp', name: 'pre.webWebp', target: 'webp', patch: { quality: 80, resize: { mode: 'long', long: 1920 } } },
  { id: 'insta', name: 'pre.instagram', target: 'jpg', patch: { quality: 90, resize: { mode: 'long', long: 1080 } } },
  { id: 'mail-photo', name: 'pre.mailPhoto', target: 'jpg', patch: { quality: 75, resize: { mode: 'long', long: 1600 } } },
  { id: 'shot-png', name: 'pre.shotPng', target: 'png', patch: { palette: true } },
  { id: 'wa-video', name: 'pre.whatsapp', target: 'mp4', patch: { targetMb: 16, height: 720 } },
  { id: 'dc-video', name: 'pre.discord', target: 'mp4', patch: { targetMb: 10, height: 720 } },
  { id: 'yt-video', name: 'pre.youtube', target: 'mp4', patch: { quality: 'high', height: 1080 } },
  { id: 'podcast', name: 'pre.podcast', target: 'mp3', patch: { quality: 'small', kbps: 96, channels: 'mono', normalize: -16 } },
  { id: 'music-flac', name: 'pre.flac', target: 'flac', patch: { compression: 8 } },
  { id: 'ringtone', name: 'pre.ringtone', target: 'aac', patch: { quality: 'balanced', end: 30, normalize: -14 } },
];
