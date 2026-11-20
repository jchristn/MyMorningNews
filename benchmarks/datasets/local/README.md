# Local corpora

Not used. BENCHMARKING.md asks platforms that ingest unstructured files to run local corpora (files kept on the
maintainer's machine, with probes and a SHA-256 manifest committed here). My Morning News does not ingest uploaded
files: it researches web pages that search returns, so its load path is search, fetch, and HTML extraction, which the
synthetic and live corpora already exercise. If the product ever accepts uploads, add the manifest and probes here and
read the root from `--local-data` or `BENCH_LOCAL_DATA`.

(c) 2026 Lattice AI LLC
