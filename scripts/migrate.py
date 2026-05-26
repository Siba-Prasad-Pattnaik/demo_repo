"""
Database migration script for Document Analyzer
"""

import psycopg2
import os
from datetime import datetime

def get_connection():
    """Get database connection"""
    return psycopg2.connect(
        host=os.getenv('DB_HOST', 'localhost'),
        database=os.getenv('DB_NAME', 'documentanalyzer'),
        user=os.getenv('DB_USER', 'postgres'),
        password=os.getenv('DB_PASSWORD', 'password'),
        port=os.getenv('DB_PORT', '5432')
    )

def run_migrations():
    """Run database migrations"""
    try:
        conn = get_connection()
        cursor = conn.cursor()

        print(f"[{datetime.now()}] Starting database migrations...")

        # Check if migration table exists
        cursor.execute("""
            CREATE TABLE IF NOT EXISTS migrations (
                id SERIAL PRIMARY KEY,
                migration_name VARCHAR(255) NOT NULL,
                executed_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
            );
        """)

        # Example migration - add indexes if they don't exist
        cursor.execute("""
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'idx_documents_upload_timestamp') THEN
                    CREATE INDEX idx_documents_upload_timestamp ON documents(upload_timestamp);
                END IF;
            END $$;
        """)

        # Record migration
        cursor.execute("""
            INSERT INTO migrations (migration_name)
            VALUES ('001_add_performance_indexes')
            ON CONFLICT DO NOTHING;
        """)

        conn.commit()
        print(f"[{datetime.now()}] Migrations completed successfully!")

    except Exception as e:
        print(f"[{datetime.now()}] Migration failed: {e}")
        if conn:
            conn.rollback()
    finally:
        if cursor:
            cursor.close()
        if conn:
            conn.close()

if __name__ == "__main__":
    run_migrations()
