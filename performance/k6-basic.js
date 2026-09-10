import http from 'k6/http';
import { check, fail, sleep } from 'k6';

export const options = {
  vus: 5,
  duration: '30s',
  thresholds: {
    http_req_failed: ['rate<0.01'],
    http_req_duration: ['p(95)<500'],
  },
};

const apiBaseUrl = __ENV.API_BASE_URL || 'http://localhost:5000';
const loginEmail = __ENV.LOGIN_EMAIL;
const loginPassword = __ENV.LOGIN_PASSWORD;

export function setup() {
  if (!loginEmail || !loginPassword) {
    fail('Set LOGIN_EMAIL and LOGIN_PASSWORD to run authenticated performance checks.');
  }

  const login = http.post(
    `${apiBaseUrl}/api/auth/login`,
    JSON.stringify({ email: loginEmail, password: loginPassword }),
    { headers: { 'Content-Type': 'application/json' } },
  );

  check(login, {
    'login is 200': (response) => response.status === 200,
    'login returns token': (response) => Boolean(response.json('accessToken')),
  });

  return { token: login.json('accessToken') };
}

export default function (data) {
  const health = http.get(`${apiBaseUrl}/health`);
  check(health, {
    'health is 200': (response) => response.status === 200,
  });

  const dashboard = http.get(`${apiBaseUrl}/api/dashboard/summary`, {
    headers: { Authorization: `Bearer ${data.token}` },
  });
  check(dashboard, {
    'dashboard is 200': (response) => response.status === 200,
    'dashboard has metrics': (response) => response.json('activeFarms') !== undefined,
  });

  sleep(1);
}