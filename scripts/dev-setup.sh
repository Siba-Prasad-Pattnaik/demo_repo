set -e

echo "Setting up development environment..."

# Check if Docker is installed
if ! command -v docker &> /dev/null; then
    echo "Docker is not installed. Please install Docker first."
    exit 1
fi

# Check if Docker Compose is installed
if ! command -v docker-compose &> /dev/null; then
    echo "Docker Compose is not installed. Please install Docker Compose first."
    exit 1
fi

# Check if .NET SDK is installed for local development
if ! command -v dotnet &> /dev/null; then
    echo "Warning: .NET SDK is not installed. Install for local development."
fi

# Check if Node.js is installed for local development
if ! command -v node &> /dev/null; then
    echo "Warning: Node.js is not installed. Install for local development."
fi

# Create uploads directory
mkdir -p uploads

# Set permissions
chmod -R 755 uploads

echo "Development environment setup complete!"
echo "Run 'docker-compose up --build' to start the application."
