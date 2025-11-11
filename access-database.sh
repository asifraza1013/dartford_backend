#!/bin/bash

# Database Access Script for Dartford Backend
# Quick access to PostgreSQL container and database

set -e

# Configuration
CONTAINER_NAME="postgres"
DB_NAME="inflan_db"
DB_USER="postgres"

# Colors
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m'

echo "=========================================="
echo "PostgreSQL Database Access"
echo "=========================================="
echo ""

# Check if container is running
if ! docker ps --format '{{.Names}}' | grep -q "^${CONTAINER_NAME}$"; then
    echo -e "${RED}✗ PostgreSQL container is not running${NC}"
    echo ""
    echo "To start the container, run:"
    echo "  docker-compose up -d postgres"
    echo ""
    exit 1
fi

echo -e "${GREEN}Connecting to database: $DB_NAME${NC}"
echo "Type '\q' or 'exit' to quit"
echo "Type '\dt' to list all tables"
echo "Type '\d table_name' to describe a table"
echo ""

# Access the database
docker exec -it $CONTAINER_NAME psql -U $DB_USER -d $DB_NAME
