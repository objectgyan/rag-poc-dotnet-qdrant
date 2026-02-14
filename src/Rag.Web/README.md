# RAG Frontend - Multi-Tenant AI Assistant

A modern, ChatGPT-like interface for the RAG POC API with multi-tenant support, built with React, TypeScript, and TailwindCSS.

## Features

- 🤖 **Dual Mode**: Switch between RAG Mode and Agent Mode
- 🏢 **Multi-Tenant**: Manage multiple workspaces with isolated data
- 💬 **ChatGPT-like UI**: Clean, modern chat interface
- 📁 **File Upload**: Drag-and-drop PDF and text file uploads
- 🔧 **Tool Visualization**: See AI agent tool usage in real-time
- 📚 **Citations**: View source documents with relevance scores
- 💰 **Cost Tracking**: Monitor API costs per query
- 🎨 **Beautiful Design**: Responsive, accessible, and polished

## Quick Start

### Prerequisites

- Node.js 18+ and npm
- RAG API running on http://localhost:5129

### Installation

```bash
# Install dependencies
npm install

# Copy environment file
cp .env.example .env

# Start development server
npm run dev
```

The app will be available at http://localhost:3000

### Build for Production

```bash
npm run build
npm run preview
```

## Project Structure

```
src/Rag.Web/
├── src/
│   ├── components/          # React components
│   │   ├── ChatInterface.tsx    # Main chat UI
│   │   ├── ChatMessage.tsx      # Message display
│   │   ├── Sidebar.tsx          # Conversations sidebar
│   │   ├── FileUpload.tsx       # File upload modal
│   │   └── TenantSwitcher.tsx   # Workspace selector
│   ├── services/            # API services
│   │   ├── rag-service.ts       # RAG endpoints
│   │   └── agent-service.ts     # Agent endpoints
│   ├── store/               # State management
│   │   └── app-store.ts         # Zustand store
│   ├── lib/                 # Utilities
│   │   ├── api-client.ts        # HTTP client
│   │   └── utils.ts             # Helper functions
│   ├── types/               # TypeScript types
│   │   └── index.ts             # Type definitions
│   ├── App.tsx              # Root component
│   ├── main.tsx             # Entry point
│   └── index.css            # Global styles
├── public/                  # Static assets
├── index.html               # HTML template
├── package.json             # Dependencies
├── vite.config.ts           # Vite configuration
├── tailwind.config.js       # Tailwind configuration
└── tsconfig.json            # TypeScript configuration
```

## Usage Guide

### Multi-Tenant Workspaces

1. Click the workspace button in the sidebar
2. Add new workspaces with custom names and API keys
3. Switch between workspaces - data is isolated per workspace
4. Default workspace is pre-configured

### Uploading Documents

1. Click the paperclip icon in the chat input
2. Drag & drop a PDF or TXT file
3. Set a document ID (auto-generated from filename)
4. Click "Upload Document"
5. Monitor progress bar
6. Document will be available for querying

### RAG Mode vs Agent Mode

**RAG Mode** (FileText icon):
- Traditional RAG: Search documents → Generate answer
- Shows citations with relevance scores
- Fast and focused on your documents

**Agent Mode** (Bot icon):
- AI agent with tool-calling capabilities
- Can search documents, GitHub, and more
- Multi-step reasoning
- Shows tool usage and reasoning traces

### Chat Features

- **New Chat**: Start fresh conversation
- **Conversation History**: All chats saved in sidebar
- **Delete Chats**: Hover over conversation → click trash icon
- **Markdown Support**: Code blocks, lists, formatting
- **Citations**: Click to see source documents
- **Tool Calls**: Expand to see arguments and results
- **Metrics**: View duration, cost, and tool usage

## Configuration

### Environment Variables

Create `.env` file:

```env
VITE_API_URL=http://localhost:5129
VITE_API_KEY=secure_password
```

### API Client

The API client automatically:
- Adds tenant headers to requests
- Handles authentication with API key
- Retries failed requests
- Logs all API calls to console

### Customization

**Colors**: Edit `tailwind.config.js` to change theme colors
**Default Tenant**: Edit `src/store/app-store.ts` initial state
**API Timeout**: Edit `src/lib/api-client.ts` timeout value

## Tech Stack

- **React 18** - UI library
- **TypeScript** - Type safety
- **Vite** - Build tool
- **TailwindCSS** - Styling
- **Zustand** - State management
- **Axios** - HTTP client
- **React Dropzone** - File uploads
- **React Markdown** - Markdown rendering
- **Lucide React** - Icons
- **date-fns** - Date formatting

## Features in Detail

### Chat Interface
- Smooth scrolling to new messages
- Auto-expanding text input
- Keyboard shortcuts (Enter to send, Shift+Enter for new line)
- Loading indicators
- Error handling

### File Upload
- Drag & drop support
- Progress bar during upload
- File size display
- Document ID auto-generation
- Success/error notifications

### Multi-Tenant
- Workspace switcher modal
- Color-coded workspaces
- Isolated conversations per workspace
- Default workspace included
- Easy workspace creation

### Agent Mode
- Real-time tool call display
- Reasoning traces
- Expandable tool arguments
- Parallel tool execution visualization
- Cost and performance metrics

## Development

### Adding New Features

1. **New API Endpoint**: Add to `services/`
2. **New Component**: Add to `components/`
3. **State Management**: Update `store/app-store.ts`
4. **Types**: Add to `types/index.ts`

### Code Style

- Use TypeScript for all files
- Follow React best practices
- Use Tailwind utility classes
- Keep components small and focused
- Add comments for complex logic

## Troubleshooting

**API Connection Issues**:
- Check API is running on http://localhost:5129
- Verify CORS is enabled on API
- Check browser console for errors

**Upload Failures**:
- Check file size (max 10MB recommended)
- Verify file type (PDF or TXT)
- Check API key is correct
- Ensure tenant is selected

**State Not Persisting**:
- Check browser local storage
- Clear cache if corrupted
- Reset state in settings

## Browser Support

- Chrome 90+
- Firefox 88+
- Safari 14+
- Edge 90+

## License

MIT License - Same as parent project

## Contributing

1. Fork the repository
2. Create feature branch
3. Make changes
4. Test thoroughly
5. Submit pull request

---

**Built with ❤️ to showcase modern AI frontend development**
