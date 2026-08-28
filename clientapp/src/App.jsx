import React, { useEffect, useState } from 'react'
import { Routes, Route, NavLink, Outlet } from 'react-router-dom'
import { api, fmtMoney, fmtDate, fmtDateTime, ISTATUS } from './api'

function Badge({ text, css }) { return <span className={`badge ${css || 'secondary'}`}>{text}</span> }
function Flash({ msg }) { return msg ? <div className={`flash ${msg.ok ? 'ok' : 'err'}`}>{msg.text}</div> : null }
function Modal({ title, onClose, wide, children }) {
  return (
    <div className="modal-bg" onClick={onClose}>
      <div className="modal" style={wide ? { maxWidth: 700 } : undefined} onClick={e => e.stopPropagation()}>
        <div className="row" style={{ marginBottom: 12 }}><h2 style={{ flex: 1, margin: 0 }}>{title}</h2>
          <button className="btn gray sm" style={{ flex: 'none' }} onClick={onClose}>Đóng</button></div>{children}
      </div>
    </div>
  )
}
function Field({ label, children }) { return <div style={{ flex: 1 }}><label>{label}</label>{children}</div> }

function Layout() {
  return (
    <>
      <nav className="nav"><span className="brand">🧾 MiniTVAN</span>
        <NavLink to="/" end>Tổng quan</NavLink><NavLink to="/invoices">Hóa đơn</NavLink>
        <NavLink to="/nnts">Người nộp thuế</NavLink><NavLink to="/lookup">Tra cứu</NavLink></nav>
      <div className="wrap"><Outlet /></div>
    </>
  )
}

function Dashboard() {
  const [d, setD] = useState(null); const [cache, setCache] = useState('')
  useEffect(() => { api.dashboard().then(r => { setD(r.data); setCache(r.cache) }) }, [])
  if (!d) return <p className="muted">Đang tải…</p>
  return (
    <>
      <h1>Tổng quan T-VAN {cache && <span className="pill">cache: {cache}</span>}</h1>
      <div className="grid kpis">
        <div className="kpi"><div className="v">{d.nnts}</div><div className="l">NNT ({d.registered} đã ĐK)</div></div>
        <div className="kpi"><div className="v">{d.invoices}</div><div className="l">Hóa đơn</div></div>
        <div className="kpi"><div className="v" style={{ color: 'var(--success)' }}>{d.accepted}</div><div className="l">CQT chấp nhận</div></div>
        <div className="kpi"><div className="v" style={{ color: 'var(--danger)' }}>{d.rejected}</div><div className="l">CQT từ chối</div></div>
        <div className="kpi"><div className="v" style={{ fontSize: 18, color: 'var(--success)' }}>{fmtMoney(d.acceptedValue)}</div><div className="l">Giá trị đã cấp mã</div></div>
      </div>
    </>
  )
}

function Invoices() {
  const [rows, setRows] = useState([]); const [status, setStatus] = useState(''); const [open, setOpen] = useState(null); const [show, setShow] = useState(false)
  const load = () => api.invoices(status === '' ? null : Number(status)).then(r => setRows(r.data))
  useEffect(() => { load() }, [status])
  return (
    <>
      <div className="toolbar"><h1 style={{ margin: 0, flex: 'none' }}>Hóa đơn điện tử</h1><div className="sp" />
        <select style={{ maxWidth: 160 }} value={status} onChange={e => setStatus(e.target.value)}><option value="">— Trạng thái —</option>{ISTATUS.map((s, i) => <option key={i} value={i}>{s}</option>)}</select>
        <button className="btn sm" style={{ flex: 'none' }} onClick={() => setShow(true)}>+ Lập HĐ</button></div>
      <div className="card" style={{ padding: 0, overflow: 'auto' }}>
        <table><thead><tr><th>Ký hiệu</th><th>Số</th><th>Người mua</th><th className="right">Tiền hàng</th><th className="right">Tổng</th><th>Mã CQT</th><th>Trạng thái</th></tr></thead>
          <tbody>{rows.map(i => (
            <tr key={i.id} style={{ cursor: 'pointer' }} onClick={() => setOpen(i.id)}>
              <td>{i.symbol}</td><td>{i.no || '—'}</td><td>{i.buyerName}</td><td className="right">{fmtMoney(i.amount)}</td>
              <td className="right"><b>{fmtMoney(i.total)}</b></td><td style={{ fontFamily: 'monospace' }}>{i.tctCode || '—'}</td><td><Badge text={i.statusText} css={i.statusCss} /></td></tr>))}
            {rows.length === 0 && <tr><td colSpan={7} className="muted" style={{ padding: 20 }}>Chưa có hóa đơn.</td></tr>}</tbody></table>
      </div>
      {open && <InvoiceDetail id={open} onClose={() => setOpen(null)} onChanged={load} />}
      {show && <InvoiceForm onClose={() => setShow(false)} onSaved={() => { setShow(false); load() }} />}
    </>
  )
}

function InvoiceDetail({ id, onClose, onChanged }) {
  const [d, setD] = useState(null); const [msg, setMsg] = useState(null)
  const load = () => api.invoice(id).then(r => setD(r.data))
  useEffect(() => { load() }, [id])
  const flash = (ok, text) => { setMsg({ ok, text }); setTimeout(() => setMsg(null), 3500) }
  const act = async (fn) => { try { const r = await fn(); flash(true, r.data.msg); load(); onChanged() } catch (e) { flash(false, e.message) } }
  if (!d) return <Modal title="…" onClose={onClose}><p className="muted">Đang tải…</p></Modal>
  const i = d.invoice
  return (
    <Modal title={`HĐ ${i.symbol} ${i.no}`} onClose={onClose} wide>
      <Flash msg={msg} />
      <div className="row" style={{ marginBottom: 8 }}><Badge text={i.statusText} css={i.statusCss} />{i.tctCode && <span className="pill" style={{ flex: 'none', fontFamily: 'monospace' }}>Mã CQT: {i.tctCode}</span>}</div>
      <dl className="dl"><dt>Người bán</dt><dd>{i.nnt} · MST {i.nntMst}</dd><dt>Người mua</dt><dd>{i.buyerName}{i.buyerMst ? ` · MST ${i.buyerMst}` : ''}</dd>
        <dt>Tiền hàng</dt><dd>{fmtMoney(i.amount)}</dd><dt>VAT ({i.vatRate}%)</dt><dd>{fmtMoney(i.vat)}</dd>
        <dt style={{ fontWeight: 700 }}>Tổng thanh toán</dt><dd style={{ fontWeight: 700, color: 'var(--brand)' }}>{fmtMoney(i.total)}</dd>
        <dt>Ngày lập</dt><dd>{fmtDate(i.issuedDate)}</dd>
        {i.rejectReason && <><dt>Lý do từ chối</dt><dd style={{ color: 'var(--danger)' }}>{i.rejectReason}</dd></>}</dl>
      <div className="section-t">Nhật ký thông điệp với TCT</div>
      <div style={{ borderLeft: '2px solid var(--line)', paddingLeft: 14, marginLeft: 6 }}>
        {d.messages.length === 0 ? <p className="muted">Chưa có thông điệp.</p> : d.messages.map((m, k) => (
          <div key={k} style={{ marginBottom: 8 }}><b>{m.dir}</b> {m.code && <span className="pill">{m.code}</span>} <span className="muted" style={{ fontSize: 12 }}>{fmtDateTime(m.createdAt)}</span><br /><span className="muted">{m.text}</span></div>))}
      </div>
      <div className="row" style={{ gap: 6, marginTop: 12 }}>
        {(i.status === 0 || i.status === 3) && <button className="btn sm" onClick={() => act(() => api.transmit(id))}>Gửi tới TCT</button>}
        {i.status !== 4 && i.status !== 2 && <button className="btn gray sm" onClick={() => act(() => api.cancel(id))}>Hủy</button>}
      </div>
    </Modal>
  )
}

function InvoiceForm({ onClose, onSaved }) {
  const [nnts, setNnts] = useState([]); const [f, setF] = useState({ nntId: '', symbol: '1C26TAA', buyerName: '', buyerMst: '', amount: 0, vatRate: 10 }); const [err, setErr] = useState('')
  useEffect(() => { api.nnts().then(r => { const reg = r.data.filter(n => n.regStatus === 2); setNnts(reg); if (reg[0]) setF(s => ({ ...s, nntId: reg[0].id })) }) }, [])
  const up = (k, v) => setF({ ...f, [k]: v })
  const save = async () => { try { if (!f.nntId) { setErr('Cần NNT đã đăng ký'); return } await api.createInvoice({ ...f, nntId: Number(f.nntId), amount: Number(f.amount), vatRate: Number(f.vatRate) }); onSaved() } catch (e) { setErr(e.message) } }
  return (
    <Modal title="Lập hóa đơn" onClose={onClose}>
      {err && <Flash msg={{ ok: false, text: err }} />}
      {nnts.length === 0 && <div className="flash err">Chưa có NNT nào ĐÃ ĐĂNG KÝ. Vào mục Người nộp thuế để đăng ký trước.</div>}
      <div className="row"><Field label="Người bán (NNT)"><select value={f.nntId} onChange={e => up('nntId', e.target.value)}>{nnts.map(n => <option key={n.id} value={n.id}>{n.name} ({n.mst})</option>)}</select></Field>
        <Field label="Ký hiệu"><input value={f.symbol} onChange={e => up('symbol', e.target.value)} /></Field></div>
      <div className="row"><Field label="Người mua"><input value={f.buyerName} onChange={e => up('buyerName', e.target.value)} /></Field>
        <Field label="MST người mua"><input value={f.buyerMst} onChange={e => up('buyerMst', e.target.value)} /></Field></div>
      <div className="row"><Field label="Tiền hàng"><input type="number" value={f.amount} onChange={e => up('amount', e.target.value)} /></Field>
        <Field label="VAT %"><input type="number" value={f.vatRate} onChange={e => up('vatRate', e.target.value)} /></Field></div>
      <div style={{ marginTop: 16 }}><button className="btn" onClick={save} disabled={nnts.length === 0}>Lập (Nháp)</button></div>
    </Modal>
  )
}

function Nnts() {
  const [rows, setRows] = useState([]); const [msg, setMsg] = useState(null); const [show, setShow] = useState(false)
  const load = () => api.nnts().then(r => setRows(r.data))
  useEffect(() => { load() }, [])
  const register = async (id) => { try { const r = await api.register(id); setMsg({ ok: true, text: r.data.msg }); load() } catch (e) { setMsg({ ok: false, text: e.message }) } }
  return (
    <>
      <div className="toolbar"><h1 style={{ margin: 0, flex: 1 }}>Người nộp thuế</h1><button className="btn sm" style={{ flex: 'none' }} onClick={() => setShow(true)}>+ Thêm NNT</button></div>
      <Flash msg={msg} />
      <div className="card" style={{ padding: 0, overflow: 'auto' }}>
        <table><thead><tr><th>MST</th><th>Tên</th><th>Địa chỉ</th><th>Trạng thái ĐK</th><th></th></tr></thead>
          <tbody>{rows.map(n => (<tr key={n.id}><td>{n.mst}</td><td>{n.name}</td><td>{n.address || '—'}</td><td><Badge text={n.regStatusText} css={n.regStatusCss} /></td>
            <td className="right">{n.regStatus !== 2 && <button className="btn sm" style={{ flex: 'none' }} onClick={() => register(n.id)}>Đăng ký TCT</button>}</td></tr>))}
            {rows.length === 0 && <tr><td colSpan={5} className="muted" style={{ padding: 20 }}>Chưa có NNT.</td></tr>}</tbody></table>
      </div>
      {show && <NntForm onClose={() => setShow(false)} onSaved={() => { setShow(false); load() }} />}
    </>
  )
}

function NntForm({ onClose, onSaved }) {
  const [f, setF] = useState({ mst: '', name: '', address: '', email: '' }); const [err, setErr] = useState('')
  const up = (k, v) => setF({ ...f, [k]: v })
  const save = async () => { try { if (!f.name) { setErr('Cần tên'); return } await api.createNnt(f); onSaved() } catch (e) { setErr(e.message) } }
  return (
    <Modal title="Thêm người nộp thuế" onClose={onClose}>
      {err && <Flash msg={{ ok: false, text: err }} />}
      <div className="row"><Field label="MST"><input value={f.mst} onChange={e => up('mst', e.target.value)} /></Field>
        <Field label="Tên *"><input value={f.name} onChange={e => up('name', e.target.value)} /></Field></div>
      <Field label="Địa chỉ"><input value={f.address} onChange={e => up('address', e.target.value)} /></Field>
      <Field label="Email"><input value={f.email} onChange={e => up('email', e.target.value)} /></Field>
      <div style={{ marginTop: 16 }}><button className="btn" onClick={save}>Lưu</button></div>
    </Modal>
  )
}

function Lookup() {
  const [code, setCode] = useState(''); const [res, setRes] = useState(null); const [err, setErr] = useState(null)
  const doLookup = async () => { try { const r = await api.lookup(code.trim()); setRes(r.data); setErr(null) } catch (e) { setErr(e.message); setRes(null) } }
  return (
    <>
      <h1>Tra cứu hóa đơn (mã CQT)</h1>
      <div className="card"><div className="row"><Field label="Mã CQT (mã tra cứu do cơ quan thuế cấp)"><input value={code} onChange={e => setCode(e.target.value)} onKeyDown={e => e.key === 'Enter' && doLookup()} /></Field>
        <div style={{ flex: 'none', alignSelf: 'flex-end' }}><button className="btn" onClick={doLookup}>Tra cứu</button></div></div></div>
      {err && <Flash msg={{ ok: false, text: err }} />}
      {res && (
        <div className="card" style={{ borderLeft: '5px solid var(--success)' }}>
          <h2>{res.symbol} {res.no}</h2>
          <dl className="dl"><dt>Người bán</dt><dd>{res.seller} · MST {res.sellerMst}</dd><dt>Người mua</dt><dd>{res.buyerName}</dd>
            <dt>Tiền hàng</dt><dd>{fmtMoney(res.amount)}</dd><dt>VAT</dt><dd>{fmtMoney(res.vat)}</dd>
            <dt>Tổng</dt><dd style={{ fontWeight: 700 }}>{fmtMoney(res.total)}</dd><dt>Mã CQT</dt><dd style={{ fontFamily: 'monospace' }}>{res.tctCode}</dd>
            <dt>Trạng thái</dt><dd>{res.status}</dd></dl>
        </div>
      )}
    </>
  )
}

export default function App() {
  return (
    <Routes>
      <Route path="/" element={<Layout />}>
        <Route index element={<Dashboard />} />
        <Route path="invoices" element={<Invoices />} />
        <Route path="nnts" element={<Nnts />} />
        <Route path="lookup" element={<Lookup />} />
      </Route>
    </Routes>
  )
}
