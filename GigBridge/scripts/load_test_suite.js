import http from 'k6/http';
import { check, sleep } from 'k6';
import { Counter, Rate, Trend } from 'k6/metrics';

// Custom Metrics for Capstone Defense Presentation
export const upstreamServerCounter = new Counter('upstream_server_count');
export const server1Counter = new Counter('server_1_requests');
export const server2Counter = new Counter('server_2_requests');
export const errorRate = new Rate('custom_error_rate');
export const apiLatency = new Trend('api_latency_ms');

export const options = {
    // 3-Minute Execution Timeline
    stages: [
        { duration: '30s', target: 10 },  // Phase 1: Warm-up (10 VUs)
        { duration: '60s', target: 50 },  // Phase 2: Baseline Load (50 VUs)
        { duration: '60s', target: 200 }, // Phase 3: Peak Target Load (200 VUs)
        { duration: '30s', target: 0 },   // Phase 4: Cool-down (Ramp down to 0)
    ],

    // Emergency Abort Circuit Breakers
    thresholds: {
        http_req_duration: ['p(95)<1500'], // Abort if 95% of requests exceed 1.5s
        custom_error_rate: ['rate<0.01'],  // Abort if error rate exceeds 1%
    },
};

const BASE_URL_API = __ENV.BASE_URL_API || 'https://api.gigbridge.id.vn';
const BASE_URL_AI = __ENV.BASE_URL_AI || 'https://ai.gigbridge.id.vn';

export default function () {
    // 1. Benchmark Backend API Gateway (.NET Kestrel)
    const resApi = http.get(`${BASE_URL_API}/health`, {
        tags: { name: 'Backend_API_Health' },
    });

    const isApiOk = check(resApi, {
        'API status is 200': (r) => r.status === 200,
    });
    errorRate.add(!isApiOk);
    apiLatency.add(resApi.timings.duration);

    // Track Nginx X-Upstream-Server Header for Load Balance Split
    const upstreamHeader = resApi.headers['X-Upstream-Server'] || resApi.headers['x-upstream-server'];
    if (upstreamHeader) {
        upstreamServerCounter.add(1);
        if (upstreamHeader.includes('127.0.0.1') || upstreamHeader.includes('localhost')) {
            server1Counter.add(1);
        } else if (upstreamHeader.includes('172.31.19.1')) {
            server2Counter.add(1);
        }
    }

    sleep(0.5);

    // 2. Benchmark AI Microservice (Python FastAPI / Mock Mode)
    const resAi = http.get(`${BASE_URL_AI}/health`, {
        tags: { name: 'AI_Service_Health' },
    });

    const isAiOk = check(resAi, {
        'AI status is 200': (r) => r.status === 200,
    });
    errorRate.add(!isAiOk);

    sleep(0.5);
}
