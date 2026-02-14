import React, { useState, useCallback } from 'react';
import { useDropzone } from 'react-dropzone';
import { X, Upload, File, Check, AlertCircle } from 'lucide-react';
import { ragService } from '@/services/rag-service';
import { useAppStore } from '@/store/app-store';
import { formatFileSize } from '@/lib/utils';

interface Props {
  onClose: () => void;
}

export const FileUpload: React.FC<Props> = ({ onClose }) => {
  const { currentTenant, addDocument, updateDocument } = useAppStore();
  const [uploading, setUploading] = useState(false);
  const [progress, setProgress] = useState(0);
  const [uploadedFile, setUploadedFile] = useState<File | null>(null);
  const [documentId, setDocumentId] = useState('');
  const [status, setStatus] = useState<'idle' | 'uploading' | 'success' | 'error'>('idle');
  const [errorMessage, setErrorMessage] = useState('');

  const onDrop = useCallback((acceptedFiles: File[]) => {
    if (acceptedFiles.length > 0) {
      const file = acceptedFiles[0];
      setUploadedFile(file);
      // Generate document ID from filename
      const id = file.name.replace(/\.[^/.]+$/, '').replace(/[^a-zA-Z0-9-_]/g, '-');
      setDocumentId(id);
      setStatus('idle');
      setErrorMessage('');
    }
  }, []);

  const { getRootProps, getInputProps, isDragActive } = useDropzone({
    onDrop,
    accept: {
      'application/pdf': ['.pdf'],
      'text/plain': ['.txt'],
    },
    multiple: false,
  });

  const handleUpload = async () => {
    if (!uploadedFile || !documentId || !currentTenant) return;

    setUploading(true);
    setStatus('uploading');
    setProgress(0);

    // Add document to store
    const docInfo = {
      id: documentId,
      name: uploadedFile.name,
      uploadedAt: new Date(),
      status: 'uploading' as const,
      tenantId: currentTenant.id,
    };
    addDocument(docInfo);

    try {
      if (uploadedFile.name.endsWith('.pdf')) {
        // Upload PDF
        await ragService.uploadPdf(uploadedFile, documentId, (prog) => {
          setProgress(prog);
        });
        updateDocument(documentId, { status: 'processing' });
      } else {
        // Upload text file
        const text = await uploadedFile.text();
        await ragService.ingestText({ documentId, text });
        updateDocument(documentId, { status: 'ready' });
      }

      setStatus('success');
      setProgress(100);
      setTimeout(() => {
        onClose();
      }, 2000);
    } catch (error) {
      console.error('Upload error:', error);
      setStatus('error');
      setErrorMessage('Upload failed. Please try again.');
      updateDocument(documentId, { status: 'error' });
    } finally {
      setUploading(false);
    }
  };

  return (
    <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50 p-4">
      <div className="bg-white rounded-2xl shadow-xl max-w-xl w-full">
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-gray-200">
          <h2 className="text-xl font-semibold text-gray-900">Upload Document</h2>
          <button
            onClick={onClose}
            className="p-2 text-gray-400 hover:text-gray-600 rounded-lg hover:bg-gray-100"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Content */}
        <div className="p-6 space-y-4">
          {/* Dropzone */}
          {!uploadedFile ? (
            <div
              {...getRootProps()}
              className={`
                border-2 border-dashed rounded-xl p-8 text-center cursor-pointer transition-colors
                ${isDragActive ? 'border-primary-500 bg-primary-50' : 'border-gray-300 hover:border-gray-400'}
              `}
            >
              <input {...getInputProps()} />
              <Upload className="w-12 h-12 mx-auto mb-4 text-gray-400" />
              {isDragActive ? (
                <p className="text-primary-600 font-medium">Drop the file here...</p>
              ) : (
                <>
                  <p className="text-gray-700 font-medium mb-2">
                    Drag & drop a file here, or click to select
                  </p>
                  <p className="text-sm text-gray-500">
                    Supports PDF and TXT files (max 10MB)
                  </p>
                </>
              )}
            </div>
          ) : (
            <>
              {/* Selected file */}
              <div className="flex items-center gap-3 p-4 bg-gray-50 rounded-lg">
                <File className="w-8 h-8 text-primary-600" />
                <div className="flex-1 min-w-0">
                  <p className="font-medium text-gray-900 truncate">{uploadedFile.name}</p>
                  <p className="text-sm text-gray-500">{formatFileSize(uploadedFile.size)}</p>
                </div>
                {!uploading && status !== 'success' && (
                  <button
                    onClick={() => {
                      setUploadedFile(null);
                      setDocumentId('');
                      setStatus('idle');
                    }}
                    className="p-1 text-gray-400 hover:text-gray-600"
                  >
                    <X className="w-5 h-5" />
                  </button>
                )}
              </div>

              {/* Document ID */}
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-2">
                  Document ID
                </label>
                <input
                  type="text"
                  value={documentId}
                  onChange={(e) => setDocumentId(e.target.value)}
                  placeholder="my-document-001"
                  disabled={uploading || status === 'success'}
                  className="w-full px-4 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-500 disabled:bg-gray-100"
                />
              </div>

              {/* Progress bar */}
              {uploading && (
                <div className="space-y-2">
                  <div className="flex justify-between text-sm text-gray-600">
                    <span>Uploading...</span>
                    <span>{progress}%</span>
                  </div>
                  <div className="h-2 bg-gray-200 rounded-full overflow-hidden">
                    <div
                      className="h-full bg-primary-600 transition-all duration-300"
                      style={{ width: `${progress}%` }}
                    />
                  </div>
                </div>
              )}

              {/* Status messages */}
              {status === 'success' && (
                <div className="flex items-center gap-2 p-3 bg-green-50 border border-green-200 rounded-lg text-green-800">
                  <Check className="w-5 h-5" />
                  <span className="text-sm font-medium">Upload successful!</span>
                </div>
              )}

              {status === 'error' && (
                <div className="flex items-center gap-2 p-3 bg-red-50 border border-red-200 rounded-lg text-red-800">
                  <AlertCircle className="w-5 h-5" />
                  <span className="text-sm font-medium">{errorMessage}</span>
                </div>
              )}

              {/* Upload button */}
              {status !== 'success' && (
                <button
                  onClick={handleUpload}
                  disabled={!documentId || uploading || !currentTenant}
                  className="w-full px-4 py-3 bg-primary-600 text-white rounded-lg hover:bg-primary-700 disabled:opacity-50 disabled:cursor-not-allowed font-medium transition-colors"
                >
                  {uploading ? 'Uploading...' : 'Upload Document'}
                </button>
              )}
            </>
          )}

          {!currentTenant && (
            <p className="text-sm text-red-600 text-center">
              Please select a tenant before uploading
            </p>
          )}
        </div>
      </div>
    </div>
  );
};
