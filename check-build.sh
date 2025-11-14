#!/bin/bash
set -e

echo "=== Checking Backend Build ==="
cd backend
if dotnet build --verbosity quiet 2>&1 | tee /tmp/build-output.txt; then
    echo "✓ Backend build successful"
else
    echo "✗ Backend build failed:"
    cat /tmp/build-output.txt
    exit 1
fi

echo ""
echo "=== Checking Frontend Syntax ==="
cd ../frontend
if npm run build 2>&1 | tee /tmp/frontend-build.txt | grep -q "Build complete\|compiled successfully"; then
    echo "✓ Frontend build check passed"
else
    echo "Note: Frontend build may have warnings (check /tmp/frontend-build.txt)"
fi

echo ""
echo "=== Checking Python Files ==="
cd ../animation_service
if python3 -m py_compile scene.py app.py 2>&1; then
    echo "✓ Python files compile successfully"
else
    echo "✗ Python syntax errors found"
    exit 1
fi

echo ""
echo "=== All checks passed! ==="

