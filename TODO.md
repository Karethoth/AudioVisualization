# TODO

- [x] Choose UI framework: Avalonia on .NET 8 for cross-platform UI.
- [x] Scaffold Avalonia solution structure (sln, main app project, shared libraries).
- [x] Prototype audio input pipeline using NAudio for Windows and provide cross-platform abstraction.
- [ ] Design effect graph data model (nodes, connections, parameters) and choose serialization format (JSON with schema versioning).
- [ ] Identify MVP node set (audio source, FFT analyzer, band splitter, gain, colorizer, image overlay, alpha/green screen, output compositor).
- [ ] Spike a headless engine that executes effect graphs and produces RGBA frames.
- [ ] Evaluate rendering backend (SkiaSharp, Direct2D, OpenGL) for compositing visual output with alpha support.
- [ ] Investigate OBS integration paths (Spout for Windows, NDI, shared texture) and define abstraction layer.
- [ ] Create plan for node editor UX (canvas interactions, inspector, live preview).
- [ ] Set up build system and CI (dotnet, cross-platform targets, automated tests).
- [ ] Draft testing strategy (unit tests for graph execution, golden-frame tests for visuals, performance benchmarks).
- [ ] Document architecture overview and coding standards.
