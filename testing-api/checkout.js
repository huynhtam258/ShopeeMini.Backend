import http from 'k6/http';
import { check, sleep } from 'k6';

const BASE = 'http://localhost:5019';
const USERS = 50;
const json = (token) => ({
  headers: { 'Content-Type': 'application/json', ...(token && { Authorization: `Bearer ${token}` }) },
});

export const options = {
  setupTimeout: '180s',
  stages: [
    { duration: '30s', target: 20 },
    { duration: '1m',  target: USERS },
    { duration: '30s', target: 0 },
  ],
  thresholds: { http_req_failed: ['rate<0.01'], 'http_req_duration{name:checkout}': ['p(95)<1000'] },
};

// Chạy 1 lần trước khi test: tạo sản phẩm (tồn kho lớn) và các tài khoản test
export function setup() {
  const admin = http.post(`${BASE}/api/auth/login`,
    JSON.stringify({ email: 'admin@shopeemini.local', password: 'Admin@123' }), json()).json('data.accessToken');

  const categoryId = http.get(`${BASE}/api/categories`).json('data')[0].id;
  const stamp = Date.now();
  const product = http.post(`${BASE}/api/admin/products`, JSON.stringify({
    categoryId, name: `LoadTest ${stamp}`, description: 'k6',
    skus: [{ skuCode: `LT-${stamp}`, variantName: 'Default', price: 100000, stockQuantity: 10000000 }],
  }), json(admin)).json('data');
  http.put(`${BASE}/api/admin/products/${product.id}/status`, JSON.stringify({ isPublic: true }), json(admin));

  const tokens = [];
  for (let i = 0; i < USERS; i++) {   // register chậm do BCrypt, nên chờ khoảng 15–30 giây
    const res = http.post(`${BASE}/api/auth/register`, JSON.stringify({
      email: `lt_${stamp}_${i}@test.local`, password: 'Test@123', fullName: `Load Test ${i}`,
    }), json());
    tokens.push(res.json('data.accessToken'));
  }
  return { tokens, skuId: product.skus[0].id };
}

export default function (data) {
  const token = data.tokens[(__VU - 1) % data.tokens.length];   // mỗi VU dùng 1 tài khoản riêng

  const add = http.post(`${BASE}/api/cart/items`,
    JSON.stringify({ productSkuId: data.skuId, quantity: 1 }), { ...json(token), tags: { name: 'add-cart' } });
  check(add, { 'add cart 2xx': (r) => r.status < 300 });

  const params = json(token);
  params.headers['Idempotency-Key'] = `${__VU}-${__ITER}-${Date.now()}`;
  params.tags = { name: 'checkout' };
  const co = http.post(`${BASE}/api/orders/checkout`, null, params);
  check(co, { 'checkout 201': (r) => r.status === 201 });

  sleep(1);
}
