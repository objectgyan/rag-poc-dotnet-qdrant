# 🧪 PHASE 4: Evaluation & Quality - RAG Evaluation Harness

## Overview

This phase implements a **comprehensive evaluation framework** for measuring and improving RAG quality. It includes:

- **Test Case Management**: Store and manage evaluation test cases
- **Automated Evaluation**: Run tests and compare actual vs expected answers
- **Hallucination Detection**: Identify when the model makes up information
- **Metrics Tracking**: Monitor accuracy, relevance, and quality over time
- **Quality Reports**: Generate detailed evaluation reports

> **Why This Matters**: Very few engineers implement evaluation systems for RAG. This makes your system **production-ready** and **trustworthy**, setting you apart from 95% of RAG implementations.

## 🎯 Features Implemented

### 1. Test Case Management
- **Store test questions** with expected answers
- **Categorize tests** by domain or difficulty
- **Define required keywords** for answer validation
- **Track expected citations** to verify retrieval quality

### 2. Evaluation Metrics

#### Semantic Similarity (0.0 - 1.0)
Measures how semantically similar the actual answer is to the expected answer using embedding-based cosine similarity.

#### Keyword Match Score (0.0 - 1.0)
Checks if the answer contains all required keywords from the expected answer.

#### Citation Accuracy (0.0 - 1.0)
Verifies if the answer retrieved the correct source documents.

#### Hallucination Score (0.0 - 1.0)
Detects if the answer contains information NOT present in the retrieved context:
- **0.0** = Fully grounded in context
- **1.0** = Completely hallucinated

#### Overall Score (0.0 - 1.0)
Weighted combination of all metrics:
- Semantic: 40%
- Keyword: 20%
- Citation: 20%
- Hallucination: 20%

### 3. Hallucination Detection

Uses **LLM-as-a-judge** approach:
1. Retrieves context documents
2. Generates answer
3. Uses another LLM call to check if answer is grounded in context
4. Identifies specific hallucinated facts

### 4. Evaluation Runs

Batch evaluation of all test cases:
- Track evaluation history
- Compare runs over time
- Identify regression in quality
- Generate aggregate metrics

## 📊 API Endpoints

### Test Case Management

#### Create Test Case
```http
POST /evaluation/test-cases
Content-Type: application/json
X-API-Key: secure_password

{
  "id": "test-001",
  "question": "What is Qdrant?",
  "expectedAnswer": "Qdrant is a vector database designed for similarity search.",
  "requiredKeywords": ["vector", "database", "similarity"],
  "expectedDocumentId": "qdrant-docs",
  "category": "basic-knowledge"
}
```

#### Get All Test Cases
```http
GET /evaluation/test-cases
X-API-Key: secure_password

# Filter by category
GET /evaluation/test-cases?category=basic-knowledge
```

#### Get Specific Test Case
```http
GET /evaluation/test-cases/test-001
X-API-Key: secure_password
```

#### Update Test Case
```http
PUT /evaluation/test-cases/test-001
Content-Type: application/json
X-API-Key: secure_password

{
  "question": "What is Qdrant used for?",
  "expectedAnswer": "Qdrant is a vector database for similarity search and RAG applications.",
  "requiredKeywords": ["vector", "database", "similarity", "RAG"]
}
```

#### Delete Test Case
```http
DELETE /evaluation/test-cases/test-001
X-API-Key: secure_password
```

### Evaluation Execution

#### Run Evaluation
```http
POST /evaluation/run
Content-Type: application/json
X-API-Key: secure_password

{
  "name": "Baseline Evaluation v1",
  "category": "basic-knowledge",
  "config": {
    "minSemanticSimilarity": 0.7,
    "minKeywordMatch": 0.6,
    "minCitationAccuracy": 0.8,
    "maxHallucinationRate": 0.2,
    "useSemanticEvaluation": true,
    "useKeywordEvaluation": true,
    "useLlmAsJudge": false
  }
}
```

**Response**:
```json
{
  "id": "run-abc123",
  "name": "Baseline Evaluation v1",
  "status": "completed",
  "totalTestCases": 10,
  "passedTestCases": 8,
  "failedTestCases": 2,
  "passRate": 0.8,
  "message": "Evaluation completed: 8/10 passed"
}
```

#### Get Evaluation Run Results
```http
GET /evaluation/runs/run-abc123
X-API-Key: secure_password
```

**Response** (detailed):
```json
{
  "id": "run-abc123",
  "name": "Baseline Evaluation v1",
  "startedAt": "2026-02-13T10:00:00Z",
  "completedAt": "2026-02-13T10:05:30Z",
  "totalTestCases": 10,
  "passedTestCases": 8,
  "failedTestCases": 2,
  "metrics": {
    "averageAccuracy": 0.82,
    "averageSemanticSimilarity": 0.85,
    "averageKeywordMatch": 0.78,
    "averageCitationAccuracy": 0.90,
    "hallucinationRate": 0.15,
    "averageResponseTimeMs": 1250,
    "averageCostPerQuery": 0.0025,
    "totalCost": 0.025,
    "passRate": 0.8
  },
  "results": [
    {
      "testCaseId": "test-001",
      "question": "What is Qdrant?",
      "expectedAnswer": "Qdrant is a vector database...",
      "actualAnswer": "Qdrant is a high-performance vector database...",
      "semanticSimilarityScore": 0.92,
      "keywordMatchScore": 1.0,
      "citationAccuracyScore": 1.0,
      "hallucinationScore": 0.0,
      "overallScore": 0.93,
      "passed": true,
      "responseTimeMs": 1100
    }
  ]
}
```

#### Get All Evaluation Runs
```http
GET /evaluation/runs
X-API-Key: secure_password
```

#### Get Aggregated Metrics
```http
GET /evaluation/metrics
X-API-Key: secure_password
```

## 🏗️ Architecture

### Data Flow

```
1. Create Test Cases
   ├── Define questions & expected answers
   ├── Specify required keywords
   └── Set category & metadata

2. Run Evaluation
   ├── Load test cases
   ├── For each test:
   │   ├── Query RAG system
   │   ├── Get actual answer + citations
   │   ├── Compute semantic similarity
   │   ├── Check keyword matches
   │   ├── Verify citation accuracy
   │   ├── Detect hallucinations (LLM-as-judge)
   │   └── Calculate overall score
   └── Aggregate metrics

3. Generate Report
   ├── Pass/Fail rate
   ├── Average scores per metric
   ├── Failed test details
   └── Trend analysis
```

### Storage

**Test Cases**: `~/.local/share/RagPoc/evaluation-test-cases.json`
**Evaluation Runs**: `~/.local/share/RagPoc/evaluation-runs.json`

> **Note**: JSON file storage is used for simplicity. For production, consider using a database (SQL Server, PostgreSQL, or MongoDB).

## 🧪 Testing Guide

### Step 1: Ingest Sample Data

First, ingest some documents to test against:

```bash
curl -X POST http://localhost:5129/ingest \
  -H "Content-Type: application/json" \
  -H "X-API-Key: secure_password" \
  -H "X-Tenant-Id: eval-test" \
  -d '{
    "documentId": "qdrant-docs",
    "text": "Qdrant is a vector database designed for similarity search. It supports high-dimensional vectors and provides efficient nearest neighbor search using HNSW algorithm. Qdrant is ideal for RAG applications, semantic search, and recommendation systems."
  }'
```

### Step 2: Create Test Cases

```bash
# Test Case 1
curl -X POST http://localhost:5129/evaluation/test-cases \
  -H "Content-Type: application/json" \
  -H "X-API-Key: secure_password" \
  -d '{
    "id": "test-001",
    "question": "What is Qdrant?",
    "expectedAnswer": "Qdrant is a vector database designed for similarity search",
    "requiredKeywords": ["vector", "database", "similarity", "search"],
    "expectedDocumentId": "qdrant-docs",
    "category": "basic-knowledge"
  }'

# Test Case 2
curl -X POST http://localhost:5129/evaluation/test-cases \
  -H "Content-Type: application/json" \
  -H "X-API-Key: secure_password" \
  -d '{
    "id": "test-002",
    "question": "What algorithm does Qdrant use?",
    "expectedAnswer": "Qdrant uses the HNSW algorithm for efficient nearest neighbor search",
    "requiredKeywords": ["HNSW", "algorithm", "search"],
    "category": "technical"
  }'
```

### Step 3: Run Evaluation

```bash
curl -X POST http://localhost:5129/evaluation/run \
  -H "Content-Type: application/json" \
  -H "X-API-Key: secure_password" \
  -d '{
    "name": "First Evaluation Run",
    "config": {
      "minSemanticSimilarity": 0.7,
      "minKeywordMatch": 0.6,
      "minCitationAccuracy": 0.8,
      "maxHallucinationRate": 0.2
    }
  }'
```

**Response**:
```json
{
  "id": "abc123",
  "name": "First Evaluation Run",
  "status": "completed",
  "totalTestCases": 2,
  "passedTestCases": 2,
  "failedTestCases": 0,
  "passRate": 1.0,
  "message": "Evaluation completed: 2/2 passed"
}
```

### Step 4: View Results

```bash
# Get detailed results
curl -X GET http://localhost:5129/evaluation/runs/abc123 \
  -H "X-API-Key: secure_password"

# Get all evaluation runs
curl -X GET http://localhost:5129/evaluation/runs \
  -H "X-API-Key: secure_password"

# Get aggregated metrics
curl -X GET http://localhost:5129/evaluation/metrics \
  -H "X-API-Key: secure_password"
```

## 📈 Metrics Explained

### Semantic Similarity

**How it works**: Converts both expected and actual answers to embeddings, then computes cosine similarity.

**Threshold**: 0.7 (70% similarity)

**Pros**:
- Captures semantic meaning
- Tolerates paraphrasing
- Language-agnostic

**Cons**:
- Computationally expensive
- May miss specific keyword requirements

### Keyword Match

**How it works**: Checks if required keywords appear in the answer.

**Threshold**: 0.6 (60% of keywords must be present)

**Pros**:
- Fast and cheap
- Ensures critical terms are included
- Easy to understand

**Cons**:
- Brittle (exact match required)
- Doesn't capture semantic meaning

### Citation Accuracy

**How it works**: Verifies if the correct source document was retrieved.

**Threshold**: 0.8 (80% accuracy)

**Why it matters**: Ensures the RAG system retrieves relevant context, not just generates plausible answers.

### Hallucination Score

**How it works**: Uses LLM to check if answer is grounded in retrieved context.

**Threshold**: 0.2 (max 20% hallucination)

**Critical for**: Ensuring trustworthiness and accuracy.

**Detection Process**:
1. Extract retrieved context chunks
2. Compare answer against context
3. Identify claims not supported by context
4. Assign hallucination score

## 💡 Best Practices

### 1. Create Diverse Test Cases

```
✅ Cover different categories:
   - Basic knowledge questions
   - Complex multi-hop reasoning
   - Edge cases and ambiguous queries
   - Domain-specific questions

✅ Include both:
   - Questions the system should answer
   - Questions outside the knowledge base
```

### 2. Set Realistic Thresholds

```
Start conservative:
- Semantic Similarity: 0.6 - 0.7
- Keyword Match: 0.5 - 0.6
- Citation Accuracy: 0.7 - 0.8
- Hallucination: 0.2 - 0.3

Tighten as system improves:
- Semantic Similarity: 0.8 - 0.9
- Keyword Match: 0.7 - 0.8
- Citation Accuracy: 0.9 - 1.0
- Hallucination: 0.1 - 0.15
```

### 3. Run Evaluations Regularly

```
Recommended schedule:
- After every major code change
- Before production deployments
- Weekly for production systems
- After ingesting new documents
```

### 4. Track Trends

```
Monitor:
- Pass rate over time
- Average response time
- Cost per query
- Hallucination rate trends
- Category-specific performance
```

### 5. Iterative Improvement

```
Workflow:
1. Run evaluation
2. Identify failing tests
3. Analyze failure reasons:
   - Poor retrieval?
   - Bad prompt engineering?
   - Insufficient context?
   - Model hallucinating?
4. Make targeted improvements
5. Re-run evaluation
6. Repeat
```

## 🔧 Configuration Options

### EvaluationConfig

```json
{
  "minSemanticSimilarity": 0.7,      // Minimum cosine similarity (0.0 - 1.0)
  "minKeywordMatch": 0.6,            // Minimum keyword match rate (0.0 - 1.0)
  "minCitationAccuracy": 0.8,        // Minimum citation accuracy (0.0 - 1.0)
  "maxHallucinationRate": 0.2,       // Maximum acceptable hallucination (0.0 - 1.0)
  "useSemanticEvaluation": true,     // Enable semantic similarity check
  "useKeywordEvaluation": true,      // Enable keyword matching
  "useLlmAsJudge": false             // Enable LLM-based hallucination detection (expensive!)
}
```

### Cost Considerations

**Hallucination Detection** (LLM-as-judge):
- **Cost**: ~2x the cost of regular query (requires extra LLM call)
- **Time**: Adds 500-1000ms per test
- **Accuracy**: Most accurate hallucination detection

**Recommendation**: 
- Use for critical evaluations
- Disable for frequent/automated runs
- Enable for final production validation

## 🎓 Advanced Use Cases

### 1. A/B Testing Different Prompts

```bash
# Test with Prompt A
curl -X POST /evaluation/run \
  -d '{ "name": "Prompt A - Detailed Instructions" }'

# Modify prompt in code

# Test with Prompt B
curl -X POST /evaluation/run \
  -d '{ "name": "Prompt B - Concise Instructions" }'

# Compare results
curl -X GET /evaluation/runs
```

### 2. Chunking Strategy Evaluation

```bash
# Test with 500-token chunks
# Modify chunking in Chunker.cs

curl -X POST /evaluation/run \
  -d '{ "name": "Chunk Size 500" }'

# Test with 1000-token chunks
# Modify chunking in Chunker.cs

curl -X POST /evaluation/run \
  -d '{ "name": "Chunk Size 1000" }'
```

### 3. Model Comparison

```bash
# Test with GPT-4
# Configure ChatModel to use GPT-4

curl -X POST /evaluation/run \
  -d '{ "name": "GPT-4 Evaluation" }'

# Test with Claude
# Configure ChatModel to use Claude

curl -X POST /evaluation/run \
  -d '{ "name": "Claude Evaluation" }'
```

### 4. Embedding Model Comparison

```bash
# Test with text-embedding-3-small
curl -X POST /evaluation/run \
  -d '{ "name": "OpenAI Small Embeddings" }'

# Test with text-embedding-3-large
curl -X POST /evaluation/run \
  -d '{ "name": "OpenAI Large Embeddings" }'
```

## 📊 Sample Evaluation Report

```
╔══════════════════════════════════════════════════════════════╗
║         RAG EVALUATION REPORT                                ║
║         Run: Baseline v1.0                                   ║
║         Date: 2026-02-13 10:30:00                           ║
╚══════════════════════════════════════════════════════════════╝

OVERALL METRICS:
  Pass Rate:              85.0% (17/20 tests)
  Average Accuracy:       82.3%
  Semantic Similarity:    84.5%
  Keyword Match:          78.9%
  Citation Accuracy:      89.2%
  Hallucination Rate:     12.4%

PERFORMANCE:
  Avg Response Time:      1,247 ms
  Avg Cost Per Query:     $0.0024
  Total Cost:             $0.048

CATEGORY BREAKDOWN:
  basic-knowledge:        100% (5/5)
  technical:              80% (4/5)
  complex-reasoning:      60% (3/5)
  edge-cases:             80% (4/5)

FAILED TESTS:
  [test-007] Complex multi-hop question
    - Semantic similarity too low: 0.62 < 0.70
    - Missing keywords: ["intermediate", "step"]
    
  [test-013] Ambiguous query
    - Citation accuracy too low: 0.50 < 0.80
    - Retrieved wrong documents
    
  [test-019] Out-of-domain question
    - Hallucination rate too high: 0.45 > 0.20
    - Model fabricated information not in context

RECOMMENDATIONS:
  ✓ Improve retrieval for complex queries
  ✓ Add disambiguation logic for ambiguous questions
  ✓ Enhance hallucination prevention in prompt
  ✓ Consider increasing context window for multi-hop questions
```

## 🚀 Production Deployment

### 1. Switch to Database Storage

Replace JSON file storage with PostgreSQL or SQL Server:

```csharp
// In Program.cs
builder.Services.AddSingleton<IEvaluationTestCaseStore, PostgresTestCaseStore>();
builder.Services.AddSingleton<EvaluationRunStore, PostgresRunStore>();
```

### 2. Add Authentication

Protect evaluation endpoints with JWT or role-based auth:

```csharp
[Authorize(Roles = "Admin,QA")]
public class EvaluationController : ControllerBase
```

### 3. Schedule Automated Runs

Use Hangfire or cron jobs to run evaluations periodically:

```csharp
RecurringJob.AddOrUpdate(
    "weekly-evaluation",
    () => _evaluationService.RunEvaluationAsync("Weekly Check", defaultConfig, null, CancellationToken.None),
    Cron.Weekly);
```

### 4. Set Up Alerting

Send alerts when evaluation quality drops:

```csharp
if (run.Metrics.PassRate < 0.8)
{
    await _alertService.SendAlert(
        $"Evaluation pass rate dropped to {run.Metrics.PassRate:P}");
}
```

### 5. Create Dashboard

Build a web dashboard to visualize trends:
- Pass rate over time
- Metric trends (line charts)
- Failed test analysis
- Cost tracking

## ✅ Summary

Phase 4 Evaluation & Quality transforms your RAG system into a **measurable, improvable, and trustworthy** solution:

- ✅ **Test Case Management**: Store and organize evaluation tests
- ✅ **Automated Evaluation**: Run comprehensive quality checks
- ✅ **Hallucination Detection**: Identify when the model makes things up
- ✅ **Metrics Tracking**: Monitor 4 key quality dimensions
- ✅ **Trend Analysis**: Track quality over time
- ✅ **Cost Monitoring**: Measure evaluation costs

**What Makes This Special**:
- 95% of RAG systems have NO evaluation framework
- Enables continuous improvement and A/B testing
- Provides objective quality metrics
- Builds trust with stakeholders
- Essential for production deployments

**Next Steps**:
1. Create test cases for your domain
2. Run baseline evaluation
3. Identify and fix failing tests
4. Set up automated evaluation schedule
5. Build evaluation dashboard
6. Integrate with CI/CD pipeline

**File Structure**:
```
evaluation-examples/
├── sample-test-cases.json         # Example test cases
└── README.md                      # This file

src/Rag.Core/
├── Models/EvaluationModels.cs     # Test cases, results, metrics
└── Services/
    ├── IEvaluationService.cs      # Evaluation interface
    ├── IEvaluationTestCaseStore.cs
    └── IHallucinationDetector.cs

src/Rag.Infrastructure/Evaluation/
├── JsonFileTestCaseStore.cs       # Test case storage
├── EvaluationRunStore.cs          # Evaluation run storage
├── RagEvaluationService.cs        # Main evaluation engine
├── LlmHallucinationDetector.cs    # Hallucination detection
└── EmbeddingSimilarityEvaluator.cs # Semantic similarity

src/Rag.Api/Controllers/
└── EvaluationController.cs        # REST API endpoints
```
