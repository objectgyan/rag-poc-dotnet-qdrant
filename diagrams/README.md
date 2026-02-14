# Diagram Images

This directory contains rendered PNG images of the Mermaid diagrams from the main README.

## 📸 How to Generate Images

### Method 1: VS Code Extension (Recommended)

1. Install [Mermaid Preview](https://marketplace.visualstudio.com/items?itemName=vstirbu.vscode-mermaid-preview) extension
2. Open the main README.md file
3. Click on any collapsed "View Mermaid Source" section
4. Right-click inside the mermaid code block
5. Select **Mermaid: Preview Diagram**
6. In the preview window, click the **Export** button
7. Save as PNG to this directory with the appropriate filename

### Method 2: Mermaid Live Editor

1. Visit https://mermaid.live/
2. Copy the Mermaid code from any collapsed section in README.md
3. Paste into the editor (it will render automatically)
4. Click **Actions** → **Export PNG** or **Export SVG**
5. Save to this directory with the appropriate filename

### Method 3: Command Line Tool

```bash
# Install mermaid-cli globally
npm install -g @mermaid-js/mermaid-cli

# Convert the ARCHITECTURE-DIAGRAMS.md file
mmdc -i ../ARCHITECTURE-DIAGRAMS.md -o .

# Or convert individual diagrams
mmdc -i diagram.mmd -o diagram-name.png
```

## 📋 Required Image Files

Create these PNG files (recommended resolution: 1920x1080 or higher):

- `system-architecture.png` - System Component Overview
- `authentication-flow.png` - JWT Authentication Flow
- `rag-query-flow.png` - RAG Query Pipeline
- `multi-tenant-isolation.png` - Multi-Tenant Data Isolation
- `agent-workflow.png` - Agent Tool Calling Workflow
- `document-ingestion.png` - Document Ingestion Pipeline

## 🎨 Export Settings

For best quality:

- **Format**: PNG
- **Resolution**: 1920x1080 or higher
- **Background**: Transparent or white
- **Scale**: 2x or 3x for crisp rendering

## 📝 Note

If you're viewing this on GitHub, the Mermaid diagrams in the main README will render automatically without needing these image files. The images are primarily for:

- Local documentation viewing
- Platforms that don't support Mermaid
- Presentations and reports
- Offline documentation

## ✨ Alternative: GitHub Native Rendering

Since GitHub supports Mermaid natively (as of 2022), the diagrams in the main README.md will render automatically when viewing the repository on GitHub. No image generation needed!
