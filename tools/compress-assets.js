/*
 * Pre-compresses the plugin's web assets at build time.
 *
 * Why build-time and not on-demand:
 *  - Jellyfin calls services.AddResponseCompression() with no options, and
 *    ResponseCompressionOptions.EnableForHttps defaults to FALSE. Anyone reaching Jellyfin over
 *    direct HTTPS (no TLS-terminating proxy) therefore downloads these assets completely
 *    uncompressed, and a plugin cannot change that setting. Serving our own Content-Encoding
 *    sidesteps it entirely.
 *  - Compressing ~500 KB with Brotli quality 11 takes seconds. Doing it per process at runtime
 *    would put that on the first user's request; doing it here costs nothing at serve time.
 *
 * Output files are named after the EMBEDDED RESOURCE NAME they should get, so the csproj can pick
 * them up with a single glob and LogicalName="%(Filename)%(Extension)".
 */
const fs = require('fs');
const path = require('path');
const zlib = require('zlib');

const [, , outDir, ...inputs] = process.argv;

if (!outDir || inputs.length === 0) {
  console.error('usage: compress-assets.js <outDir> <resourceName=file> ...');
  process.exit(1);
}

fs.mkdirSync(outDir, { recursive: true });

let total = 0;
let totalBr = 0;
let totalGz = 0;

for (const pair of inputs) {
  const idx = pair.indexOf('=');
  if (idx < 0) {
    console.error('bad argument (expected resourceName=file): ' + pair);
    process.exit(1);
  }

  const resourceName = pair.slice(0, idx);
  const file = pair.slice(idx + 1);

  if (!fs.existsSync(file)) {
    console.error('missing input: ' + file);
    process.exit(1);
  }

  const raw = fs.readFileSync(file);

  const br = zlib.brotliCompressSync(raw, {
    params: {
      [zlib.constants.BROTLI_PARAM_QUALITY]: zlib.constants.BROTLI_MAX_QUALITY,
      [zlib.constants.BROTLI_PARAM_SIZE_HINT]: raw.length,
    },
  });

  const gz = zlib.gzipSync(raw, { level: zlib.constants.Z_BEST_COMPRESSION });

  fs.writeFileSync(path.join(outDir, resourceName + '.br'), br);
  fs.writeFileSync(path.join(outDir, resourceName + '.gz'), gz);

  total += raw.length;
  totalBr += br.length;
  totalGz += gz.length;

  console.log(
    '  %s: %d -> br %d (%d%%), gz %d (%d%%)',
    path.basename(file),
    raw.length,
    br.length,
    Math.round((100 * br.length) / raw.length),
    gz.length,
    Math.round((100 * gz.length) / raw.length)
  );
}

console.log(
  'total: %d -> br %d (%d%%), gz %d (%d%%)',
  total,
  totalBr,
  Math.round((100 * totalBr) / total),
  totalGz,
  Math.round((100 * totalGz) / total)
);
