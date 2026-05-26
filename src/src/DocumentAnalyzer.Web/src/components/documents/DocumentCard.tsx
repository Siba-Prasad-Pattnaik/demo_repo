import React from 'react';
import { Link } from 'react-router-dom';
import { FileText, Calendar, User } from 'lucide-react';
import { formatDate } from '../../utils/formatUtils';

interface Document {
  id: string;
  title: string;
  fileName: string;
  uploadDate: string;
  uploadedBy: string;
  size: number;
  status: 'processing' | 'completed' | 'error';
}

interface DocumentCardProps {
  document: Document;
}

const DocumentCard: React.FC<DocumentCardProps> = ({ document }) => {
  const getStatusColor = (status: string) => {
    switch (status) {
      case 'completed':
        return 'bg-green-100 text-green-800';
      case 'processing':
        return 'bg-yellow-100 text-yellow-800';
      case 'error':
        return 'bg-red-100 text-red-800';
      default:
        return 'bg-gray-100 text-gray-800';
    }
  };

  return (
    <Link to={`/documents/${document.id}`} className="block">
      <div className="card hover:shadow-md transition-shadow">
        <div className="flex items-start justify-between">
          <div className="flex items-start space-x-3">
            <div className="flex-shrink-0">
              <FileText className="w-8 h-8 text-gray-400" />
            </div>
            <div className="flex-1 min-w-0">
              <h3 className="text-lg font-medium text-gray-900 truncate">
                {document.title || document.fileName}
              </h3>
              <p className="text-sm text-gray-500 truncate">{document.fileName}</p>
              <div className="mt-2 flex items-center space-x-4 text-sm text-gray-500">
                <div className="flex items-center">
                  <Calendar className="w-4 h-4 mr-1" />
                  {formatDate(document.uploadDate)}
                </div>
                <div className="flex items-center">
                  <User className="w-4 h-4 mr-1" />
                  {document.uploadedBy}
                </div>
              </div>
            </div>
          </div>
          <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${getStatusColor(document.status)}`}>
            {document.status}
          </span>
        </div>
      </div>
    </Link>
  );
};

export default DocumentCard;
