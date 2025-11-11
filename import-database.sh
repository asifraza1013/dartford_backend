#!/bin/bash

# Database Import Script for Dartford Backend
# This script helps you import the database schema into PostgreSQL container

set -e  # Exit on error

echo "=========================================="
echo "PostgreSQL Database Import Script"
echo "=========================================="
echo ""

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Configuration from docker-compose.yml
CONTAINER_NAME="dartford_backend-postgres-1"
DB_NAME="inflan_db"
DB_USER="postgres"
DB_PASSWORD="postgres123"
SCHEMA_FILE="database_schema.sql"

# Function to check if container is running
check_container() {
    if docker ps --format '{{.Names}}' | grep -q "^${CONTAINER_NAME}$"; then
        echo -e "${GREEN}✓ PostgreSQL container is running${NC}"
        return 0
    else
        echo -e "${RED}✗ PostgreSQL container is not running${NC}"
        return 1
    fi
}

# Function to start containers
start_containers() {
    echo -e "${YELLOW}Starting Docker containers...${NC}"
    docker-compose up -d postgres
    echo "Waiting for PostgreSQL to be ready..."
    sleep 5

    # Wait for PostgreSQL to be ready
    for i in {1..30}; do
        if docker exec $CONTAINER_NAME pg_isready -U $DB_USER > /dev/null 2>&1; then
            echo -e "${GREEN}✓ PostgreSQL is ready${NC}"
            return 0
        fi
        echo "Waiting for PostgreSQL... ($i/30)"
        sleep 1
    done

    echo -e "${RED}✗ PostgreSQL failed to start${NC}"
    return 1
}

# Main execution
echo "Step 1: Checking container status..."
if ! check_container; then
    echo ""
    read -p "Container not running. Would you like to start it? (y/n) " -n 1 -r
    echo ""
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        start_containers || exit 1
    else
        echo -e "${RED}Cannot proceed without running container. Exiting.${NC}"
        exit 1
    fi
fi

echo ""
echo "Step 2: Checking schema file..."
if [ ! -f "$SCHEMA_FILE" ]; then
    echo -e "${RED}✗ Schema file '$SCHEMA_FILE' not found!${NC}"
    exit 1
fi
echo -e "${GREEN}✓ Schema file found${NC}"

echo ""
echo "Step 3: Importing database schema..."
echo "Database: $DB_NAME"
echo "User: $DB_USER"
echo ""

# Import the schema using pipe (works on both Windows and Mac)
if cat $SCHEMA_FILE | docker exec -i $CONTAINER_NAME psql -U $DB_USER -d $DB_NAME; then
    echo ""
    echo -e "${GREEN}=========================================="
    echo "✓ Database schema imported successfully!"
    echo "==========================================${NC}"

    # Show table list
    echo ""
    echo "Tables in database:"
    docker exec -i $CONTAINER_NAME psql -U $DB_USER -d $DB_NAME -c "\dt"
else
    echo ""
    echo -e "${RED}✗ Failed to import schema${NC}"
    exit 1
fi

echo ""
echo -e "${GREEN}All done!${NC}"
echo ""
echo "To access the database, use:"
echo "  ./access-database.sh"
echo ""
