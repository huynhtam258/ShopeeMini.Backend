import http from 'k6/http';
import { check, sleep } from 'k6';

const BASE = 'http://localhost:5019';

export const options = {
  stages: [               // tăng dần số user ảo (VU) để tìm điểm gãy
    { duration: '30s', target: 50 },
    { duration: '1m',  target: 50 },
    { duration: '30s', target: 200 },
    { duration: '1m',  target: 200 },
    { duration: '30s', target: 500 },
    { duration: '1m',  target: 500 },
    { duration: '30s', target: 0 },
  ],
  thresholds: {
    http_req_failed:   ['rate<0.01'],   // dưới 1% lỗi
    http_req_duration: ['p(95)<500'],   // 95% request nhanh hơn 500ms
  },
};

export default function () {
  const page = Math.floor(Math.random() * 3) + 1;
  const list = http.get(`${BASE}/api/products?page=${page}&pageSize=20`, { tags: { name: 'list' } });
  check(list, { 'list 200': (r) => r.status === 200 });

  const items = list.json('data.items') || [];
  if (items.length > 0) {
    const id = items[Math.floor(Math.random() * items.length)].id;
    const detail = http.get(`${BASE}/api/products/${id}`, { tags: { name: 'detail' } });
    check(detail, { 'detail 200': (r) => r.status === 200 });
  }
  sleep(Math.random() * 2 + 1);   // user "suy nghĩ" 1–3 giây
}
