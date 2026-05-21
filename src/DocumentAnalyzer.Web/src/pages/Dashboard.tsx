import React, { useState, useEffect } from 'react';
import { DocumentUpload } from '../components/DocumentUpload';
import { documentService, Document } from '../services/documentService';
import { authService } from '../services/authService';
import { escapeHtml } from '../utils/validation';

export const Dashboard: React.FC = () => {
  const [documents, setDocuments] = useState<Document[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [selectedDocument, setSelectedDocument] = useState<Document | null>(null);

  useEffect(() => {
    loadDocuments();
  }, []);

  const loadDocuments = async () => {
    try {
      setLoading(true);
      const docs = await documentService.getDocuments();
      setDocuments(docs);
    } catch (err: any) {
      setError(err.message || 'Failed to load documents');
    } finally {
      setLoading(false);
    }
  };

  const handleUploadSuccess = (document: Document) => {
    setDocuments(prev => [document, ...prev]);
    setError('');
  };

  const handleUploadError = (errorMessage: string) => {
    setError(errorMessage);
  };

  const handleDeleteDocument = async (id: string) => {
    if (!window.confirm('Are you sure you want to delete this document?')) {
      return;
    }

    try {
      await documentService.deleteDocument(id);
      setDocuments(prev => prev.filter(doc => doc.id !== id));
      if (selectedDocument?.id === id) {
        setSelectedDocument(null);
      }
    } catch (err: any) {
      setError(err.message || 'Failed to delete document');
    }
  };

  const handleAnalyzeDocument = async (document: Document) => {
    try {
      setError('');
      await documentService.analyzeDocument(document.id);
      // Reload documents to get updated status
      await loadDocuments();
    } catch (err: any) {
      setError(err.message || 'Failed to analyze document');
    }
  };

  const handleLogout = async () => {
    await authService.logout();
    window.location.href = '/login';
  };

  const formatFileSize = (bytes: number): string => {
    if (bytes === 0) return '0 Bytes';
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
  };

  return (
    <div className="dashboard-container">
      <header className="dashboard-header">
        <h1>Document Analyzer</h1>
        <button onClick={handleLogout} className="logout-button">
          Logout
        </button>
      </header>

      {error && (
        <div className="error-banner" role="alert">
          {escapeHtml(error)}
          <button onClick={() => setError('')} className="close-error">

          </button>
        </div>
      )}

      <main className="dashboard-main">
        <section className="upload-section">
          <h2>Upload New Document</h2>
          <DocumentUpload
            onUploadSuccess={handleUploadSuccess}
            onUploadError={handleUploadError}
          />
        </section>

        <section className="documents-section">
          <h2>Your Documents</h2>

          {loading ? (
            <div className="loading">Loading documents...</div>
          ) : documents.length === 0 ? (
            <div className="empty-state">
              <p>No documents uploaded yet. Upload your first document above.</p>
            </div>
          ) : (
            <div className="documents-grid">
              {documents.map((document) => (
                <div key={document.id} className="document-card">
                  <div className="document-header">
                    <h3 title={document.name}>
                      {escapeHtml(document.name)}
                    </h3>
                    <div className="document-actions">
                      <button
                        onClick={() => setSelectedDocument(document)}
                        className="view-button"
                      >
                        View
                      </button>
                      <button
                        onClick={() => handleDeleteDocument(document.id)}
                        className="delete-button"
                      >
                        Delete
                      </button>
                    </div>
                  </div>

                  <div className="document-metadata">
                    <p>Size: {formatFileSize(document.size)}</p>
                    <p>Type: {escapeHtml(document.type)}</p>
                    <p>Uploaded: {new Date(document.uploadedAt).toLocaleDateString()}</p>
                    <p className={`status status-${document.status}`}>
                      Status: {document.status}
                    </p>
                  </div>

                  {document.status === 'completed' && !document.analysis && (
                    <button
                      onClick={() => handleAnalyzeDocument(document)}
                      className="analyze-button"
                    >
                      Analyze Document
                    </button>
                  )}

                  {document.analysis && (
                    <div className="analysis-preview">
                      <h4>Analysis Summary</h4>
                      <p>{escapeHtml(document.analysis.summary.substring(0, 150))}...</p>
                      <p>Sentiment: {document.analysis.sentiment}</p>
                    </div>
                  )}
                </div>
              ))}
            </div>
          )}
        </section>
      </main>
    </div>
  );
};
