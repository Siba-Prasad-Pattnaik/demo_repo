import React, { useCallback, useState } from 'react';
import { useDropzone } from 'react-dropzone';
import { documentService } from '../services/documentService';
import { logSecurityEvent } from '../utils/security';

interface DocumentUploadProps {
  onUploadSuccess?: (document: any) => void;
  onUploadError?: (error: string) => void;
}

export const DocumentUpload: React.FC<DocumentUploadProps> = ({
  onUploadSuccess,
  onUploadError,
}) => {
  const [uploading, setUploading] = useState(false);
  const [progress, setProgress] = useState(0);

  const onDrop = useCallback(async (acceptedFiles: File[]) => {
    if (acceptedFiles.length === 0) {
      onUploadError?.('No valid files selected');
      return;
    }

    const file = acceptedFiles[0];

    try {
      setUploading(true);
      setProgress(0);

      // Security logging
      logSecurityEvent('FILE_UPLOAD_STARTED', {
        filename: file.name,
        size: file.size,
        type: file.type,
      });

      const document = await documentService.uploadDocument({ file });

      logSecurityEvent('FILE_UPLOAD_SUCCESS', {
        documentId: document.id,
        filename: file.name,
      });

      onUploadSuccess?.(document);
    } catch (error: any) {
      logSecurityEvent('FILE_UPLOAD_FAILED', {
        filename: file.name,
        error: error.message,
      });
      onUploadError?.(error.message);
    } finally {
      setUploading(false);
      setProgress(0);
    }
  }, [onUploadSuccess, onUploadError]);

  const { getRootProps, getInputProps, isDragActive, rejectedFiles } = useDropzone({
    onDrop,
    accept: {
      'application/pdf': ['.pdf'],
      'application/msword': ['.doc'],
      'application/vnd.openxmlformats-officedocument.wordprocessingml.document': ['.docx'],
      'text/plain': ['.txt'],
    },
    maxSize: 50 * 1024 * 1024, // 50MB
    maxFiles: 1,
    disabled: uploading,
  });

  return (
    <div className="document-upload-container">
      <div
        {...getRootProps()}
        className={`dropzone ${isDragActive ? 'active' : ''} ${uploading ? 'uploading' : ''}`}
      >
        <input {...getInputProps()} />

        {uploading ? (
          <div className="upload-progress">
            <p>Uploading document...</p>
            <div className="progress-bar">
              <div
                className="progress-fill"
                style={{ width: `${progress}%` }}
              />
            </div>
          </div>
        ) : (
          <div className="upload-prompt">
            {isDragActive ? (
              <p>Drop the document here...</p>
            ) : (
              <div>
                <p>Drag & drop a document here, or click to select</p>
                <p className="upload-restrictions">
                  Supported formats: PDF, DOC, DOCX, TXT<br />
                  Maximum size: 50MB
                </p>
              </div>
            )}
          </div>
        )}
      </div>

      {rejectedFiles.length > 0 && (
        <div className="upload-errors">
          <h4>Upload Errors:</h4>
          {rejectedFiles.map(({ file, errors }) => (
            <div key={file.name} className="error-item">
              <strong>{file.name}</strong>
              <ul>
                {errors.map((error) => (
                  <li key={error.code}>{error.message}</li>
                ))}
              </ul>
            </div>
          ))}
        </div>
      )}
    </div>
  );
};
