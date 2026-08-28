const base = '/api/v1'
async function req(path, opts = {}) {
  const res = await fetch(base + path, {
    headers: { 'Content-Type': 'application/json' }, credentials: 'same-origin',
    ...opts, body: opts.body ? JSON.stringify(opts.body) : undefined
  })
  const text = await res.text(); const data = text ? JSON.parse(text) : null
  if (!res.ok) throw new Error(data?.error || `Lỗi ${res.status}`)
  return { data, cache: res.headers.get('X-Cache') }
}
export const api = {
  dashboard: () => req('/dashboard'),
  nnts: () => req('/nnts'),
  createNnt: (b) => req('/nnts', { method: 'POST', body: b }),
  register: (id) => req(`/nnts/${id}/register`, { method: 'POST' }),
  invoices: (status, nntId) => req(`/invoices?${status != null ? `status=${status}&` : ''}${nntId ? `nntId=${nntId}` : ''}`),
  invoice: (id) => req(`/invoices/${id}`),
  createInvoice: (b) => req('/invoices', { method: 'POST', body: b }),
  transmit: (id) => req(`/invoices/${id}/transmit`, { method: 'POST' }),
  cancel: (id) => req(`/invoices/${id}/cancel`, { method: 'POST' }),
  lookup: (code) => req(`/lookup/${encodeURIComponent(code)}`)
}
export const fmtMoney = (n) => (n ?? 0).toLocaleString('vi-VN') + 'đ'
export const fmtDate = (s) => s ? new Date(s).toLocaleDateString('vi-VN') : '—'
export const fmtDateTime = (s) => s ? new Date(s).toLocaleString('vi-VN') : '—'
export const ISTATUS = ['Nháp', 'Đang gửi TCT', 'CQT chấp nhận', 'CQT từ chối', 'Đã hủy']
