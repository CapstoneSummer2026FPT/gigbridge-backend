import http from 'k6/http';
import ws from 'k6/ws';
import { check, sleep } from 'k6';
import { Rate, Trend } from 'k6/metrics';

// PowerShell usage (keep the short-lived token out of the command line):
//   $env:SIGNALR_ACCESS_TOKEN = '<ephemeral access token>'
//   k6 run .\scripts\signalr_affinity_smoke.js
// Optional inputs: BASE_URL, HUB_PATH, ATTEMPTS, and HANDSHAKE_TIMEOUT_MS.

const accessToken = __ENV.SIGNALR_ACCESS_TOKEN;
const baseUrl = (__ENV.BASE_URL || 'https://api.gigbridge.id.vn').replace(/\/$/, '');
const hubPath = normalizeHubPath(__ENV.HUB_PATH || '/hubs/chat');
const attempts = parsePositiveInteger(__ENV.ATTEMPTS || '50', 'ATTEMPTS');
const timeoutMilliseconds = parsePositiveInteger(
    __ENV.HANDSHAKE_TIMEOUT_MS || '10000',
    'HANDSHAKE_TIMEOUT_MS');

if (!accessToken) {
    throw new Error('SIGNALR_ACCESS_TOKEN is required. Supply it through the environment.');
}

if (!/^https?:\/\//i.test(baseUrl)) {
    throw new Error('BASE_URL must start with http:// or https://.');
}

export const connectionFailures = new Rate('signalr_connection_failures');
export const connectionDuration = new Trend('signalr_connection_duration_ms', true);

export const options = {
    vus: 1,
    iterations: attempts,
    thresholds: {
        checks: ['rate==1'],
        signalr_connection_failures: ['rate==0'],
    },
};

export default function () {
    const startedAt = Date.now();
    const authorizationHeaders = {
        Authorization: `Bearer ${accessToken}`,
        Origin: 'https://gigbridge.id.vn',
    };

    const negotiateResponse = http.post(
        `${baseUrl}${hubPath}/negotiate?negotiateVersion=1`,
        null,
        {
            headers: authorizationHeaders,
            redirects: 0,
            tags: { name: 'SignalR negotiate' },
        });

    const negotiateSucceeded = check(negotiateResponse, {
        'SignalR negotiate returns 200': response => response.status === 200,
    });

    if (!negotiateSucceeded) {
        connectionFailures.add(true);
        connectionDuration.add(Date.now() - startedAt);
        sleep(0.1);
        return;
    }

    let connectionToken;
    try {
        connectionToken = negotiateResponse.json('connectionToken');
    } catch (_) {
        connectionToken = null;
    }

    const tokenPresent = check(connectionToken, {
        'SignalR negotiate returns a connection token': value =>
            typeof value === 'string' && value.length > 0,
    });

    if (!tokenPresent) {
        connectionFailures.add(true);
        connectionDuration.add(Date.now() - startedAt);
        sleep(0.1);
        return;
    }

    const webSocketBaseUrl = baseUrl.replace(/^http/i, 'ws');
    const socketUrl = `${webSocketBaseUrl}${hubPath}?id=${encodeURIComponent(connectionToken)}`;
    let handshakeAccepted = false;
    let socketErrored = false;

    // k6 can authenticate a WebSocket with an Authorization header. Keeping the
    // bearer token out of the URL prevents failed smoke tests from printing it.
    const socketResponse = ws.connect(
        socketUrl,
        {
            headers: authorizationHeaders,
            tags: { name: 'SignalR WebSocket transport' },
        },
        socket => {
            socket.on('open', () => {
                socket.send('{"protocol":"json","version":1}\u001e');
            });

            socket.on('message', data => {
                const frames = String(data)
                    .split('\u001e')
                    .filter(frame => frame.length > 0);

                for (const frame of frames) {
                    try {
                        const message = JSON.parse(frame);
                        if (message && Object.keys(message).length === 0) {
                            handshakeAccepted = true;
                            socket.close();
                            return;
                        }
                    } catch (_) {
                        // Ignore non-JSON frames; the handshake assertion below fails safely.
                    }
                }
            });

            socket.on('error', () => {
                socketErrored = true;
            });

            socket.setTimeout(() => socket.close(), timeoutMilliseconds);
        });

    const connected = check(
        {
            status: socketResponse && socketResponse.status,
            handshakeAccepted,
            socketErrored,
        },
        {
            'SignalR WebSocket upgrades with status 101': result => result.status === 101,
            'SignalR protocol handshake succeeds': result =>
                result.handshakeAccepted && !result.socketErrored,
        });

    connectionFailures.add(!connected);
    connectionDuration.add(Date.now() - startedAt);
    sleep(0.1);
}

function normalizeHubPath(value) {
    const trimmed = value.trim().replace(/\/$/, '');
    if (!trimmed || trimmed.includes('?') || trimmed.includes('#')) {
        throw new Error('HUB_PATH must be a path without a query string or fragment.');
    }

    return trimmed.startsWith('/') ? trimmed : `/${trimmed}`;
}

function parsePositiveInteger(value, name) {
    const parsed = Number.parseInt(value, 10);
    if (!Number.isInteger(parsed) || parsed <= 0) {
        throw new Error(`${name} must be a positive integer.`);
    }

    return parsed;
}
