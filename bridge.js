/* KingR9 Tools — bridge.js : เชื่อม HTML UI ↔ C# (WebView2) ฉบับหน้าแบบ R9
   - เปิดในเบราว์เซอร์ปกติ: ไม่ทำอะไร (หน้านิ่ง preview ได้)
   - รันในแอป (KR_HOSTED): ทุกปุ่ม/checkbox ยิงฟังก์ชัน C# จริง
   - RPC ผ่าน postMessage — C# ย้ายงานหนัก (สคริปต์/HTTP/WMI) ไป background thread
     ทั้งหมด → หน้าเว็บและหน้าต่างแอปไม่ค้างแม้คำสั่งจะใช้เวลานาน */
(function () {
  const HOSTED = window.KR_HOSTED === true;
  const wv = HOSTED && window.chrome && window.chrome.webview ? window.chrome.webview : null;
  const handlers = {};

  window.__krDispatch = function (d) { fire(d && d.event, d); };
  function on(ev, fn) { handlers[ev] = fn; }
  function fire(ev, d) { if (handlers[ev]) { try { handlers[ev](d); } catch (e) {} } }

  function toast(msg) {
    const t = document.getElementById('toast');
    if (!t) { console.log('[toast]', msg); return; }
    t.textContent = msg;
    t.classList.add('on');
    clearTimeout(t._tm);
    t._tm = setTimeout(function () { t.classList.remove('on'); }, 2600);
  }

  /* progress ของ Optimize/Restore (JS poll ทุก 0.4 วิ — ผ่าน rpc แบบไม่บล็อก) */
  let progressTimer = null;
  function pollProgress() {
    if (progressTimer) return;
    progressTimer = setInterval(function () {
      rpc('progress').then(function (p) {
        if (p.running) {
          fire('busy', { on: true });
          fire('applyProgress', { pct: p.pct || 0 });
        } else {
          if (p.done) fire('applyDone', { ok: p.ok, fail: p.fail });
          clearInterval(progressTimer);
          progressTimer = null;
          fire('busy', { on: false });
        }
      }).catch(function () {});
    }, 400);
  }

  /* ---- RPC แบบไม่บล็อก: postMessage → C# รับที่ WebMessageReceived แล้วย้ายงานหนัก
     ไป background thread → ผลลัพธ์ส่งกลับเป็น { __krRes: id, res: {...} } ---- */
  const pending = {};
  let rpcSeq = 1;
  if (wv) {
    wv.addEventListener('message', function (e) {
      const d = e.data;
      if (!d || d.__krRes === undefined) return;
      const cb = pending[d.__krRes];
      if (!cb) return;
      delete pending[d.__krRes];
      cb(d.res || {});
    });
  }
  function rpc(type, payload) {
    return new Promise(function (resolve, reject) {
      if (!wv) return reject(new Error('not hosted'));
      const id = rpcSeq++;
      pending[id] = resolve;
      try {
        wv.postMessage({ __kr: id, type: type, payload: payload || {} });
        if (type === 'optimize' || type === 'restoreAll') pollProgress();
      } catch (e) { delete pending[id]; reject(e); }
    });
  }

  /* ---- busy overlay: หน้าโหลด + % สำหรับปุ่ม apply ทุกตัว ---- */
  let busyEl = null, jobTimer = null, busySeq = 0, busyPct = 0, busyHideTimer = null;
  function busyShow(label, pollType) {
    busySeq++;   // งานใหม่เข้ามา = งานเก่าห้ามปิด overlay ทับ
    if (!document.body) return;
    if (busyHideTimer) { clearTimeout(busyHideTimer); busyHideTimer = null; }
    if (!busyEl) {
      busyEl = document.createElement('div');
      busyEl.style.cssText = 'position:fixed;inset:0;z-index:10000;display:flex;align-items:center;justify-content:center;background:rgba(10,8,16,.55);opacity:0;transition:opacity .2s;pointer-events:none;';
      busyEl.innerHTML =
        '<style>@keyframes kbspin{to{transform:rotate(360deg)}}</style>' +
        '<div style="min-width:280px;max-width:86vw;background:rgba(22,18,34,.97);border:1px solid rgba(255,255,255,.09);border-radius:18px;padding:22px 26px;text-align:center;box-shadow:0 18px 60px rgba(0,0,0,.5)">' +
        '<div style="width:34px;height:34px;margin:0 auto 12px;border-radius:50%;border:3px solid rgba(255,255,255,.12);border-top-color:#e879f9;animation:kbspin .8s linear infinite"></div>' +
        '<div id="kbLabel" style="font:600 14px \'Space Grotesk\',sans-serif;color:#f2f0f7;margin-bottom:10px">กำลังทำงาน...</div>' +
        '<div style="height:8px;border-radius:99px;background:rgba(255,255,255,.08);overflow:hidden"><div id="kbBar" style="height:100%;width:0%;border-radius:99px;background:linear-gradient(90deg,#a78bfa,#f0abfc);transition:width .3s"></div></div>' +
        '<div id="kbPct" style="font:600 12px \'Space Grotesk\',sans-serif;color:rgba(242,240,247,.55);margin-top:8px">0%</div>' +
        '</div>';
      document.body.appendChild(busyEl);
    }
    const already = busyEl.style.opacity === '1';
    if (already) {
      /* งานต่อเนื่อง (chain) — เปลี่ยนแค่ข้อความ แถบ % ไหลต่อ ไม่รีเซ็ตกลับ 0 */
      if (label) busyEl.querySelector('#kbLabel').textContent = label;
    } else {
      busyPct = 0;
      busyEl.querySelector('#kbLabel').textContent = label || 'กำลังทำงาน...';
      busyEl.querySelector('#kbBar').style.width = '0%';
      busyEl.querySelector('#kbPct').textContent = '0%';
    }
    busyEl.style.pointerEvents = 'auto';
    busyEl.style.opacity = '1';
    if (already) return;   // poll ตัวเดิมยังทำงานอยู่
    if (jobTimer) clearInterval(jobTimer);
    jobTimer = setInterval(function () {
      rpc(pollType || 'job').then(function (j) {
        if (!busyEl || busyEl.style.opacity !== '1') return;
        if (typeof j.pct === 'number' && j.pct > busyPct) {   // % ไหลขึ้นอย่างเดียว ไม่ถอยหลัง
          busyPct = j.pct;
          busyEl.querySelector('#kbBar').style.width = busyPct + '%';
          busyEl.querySelector('#kbPct').textContent = busyPct + '%';
        }
        if (j.label) busyEl.querySelector('#kbLabel').textContent = j.label;
      }).catch(function () {});
    }, 400);
  }
  function busyHide() {
    if (jobTimer) { clearInterval(jobTimer); jobTimer = null; }
    if (busyHideTimer) { clearTimeout(busyHideTimer); busyHideTimer = null; }
    if (busyEl) { busyEl.style.opacity = '0'; busyEl.style.pointerEvents = 'none'; }
  }
  /* ซ่อนแบบเผื่อเวลา — ถ้ามีงานใหม่เข้ามาแทนระหว่างรอ งานใหม่จะเป็นคนจัดการเอง */
  function busyHideSoon(ms) {
    const mySeq = busySeq;
    if (busyHideTimer) clearTimeout(busyHideTimer);
    busyHideTimer = setTimeout(function () {
      busyHideTimer = null;
      if (busySeq === mySeq) busyHide();
    }, ms || 500);
  }
  /* ครอบ rpc พวก apply — โชว์หน้าโหลดระหว่างทำงาน + toast ผลตอนจบเสมอ (หายปัญหา "กดแล้วเงียบ")
     จบงาน = เด้งแถบไป 100% ค้างไว้ ~0.5 วิ ก่อนซ่อน (หน้าโหลดจะไม่หายก่อนถึง 100)
     งานลูกโซ่: ส่ง { last: false } ในขั้นที่ไม่ใช่ขั้นสุดท้าย → แถบไหลต่อ ไม่เด้ง 100 ไม่ปิดกลางคัน */
  function applyRpc(type, payload, label, opts) {
    opts = opts || {};
    busyShow(label);
    const mySeq = busySeq;
    return rpc(type, payload).then(function (r) {
      if (r && r.ok === false) {           // งานไม่ได้เริ่ม (เช่น มีงานอื่นรันอยู่) — ซ่อนทันที
        busyHide();
        KR.toast(r.msg || 'ไม่สำเร็จ — ลองใหม่');
        return r;
      }
      if (opts.last === false) return r;   // ขั้นกลางของงานลูกโซ่ — ขั้นถัดไปเปลี่ยนข้อความต่อเลย
      if (busySeq === mySeq && busyEl) {   // เด้งแถบไป 100% ให้เห็นก่อน
        try {
          busyPct = 100;
          busyEl.querySelector('#kbBar').style.width = '100%';
          busyEl.querySelector('#kbPct').textContent = '100%';
        } catch (e) {}
      }
      setTimeout(function () {
        if (busySeq !== mySeq) return;     // มีงานใหม่มาแทน (chain) — งานใหม่จัดการเอง
        busyHide();
        KR.toast(r && r.msg ? r.msg : 'เสร็จเรียบร้อย ✓');
      }, 500);
      return r;
    }).catch(function () {
      if (busySeq === mySeq) busyHide();
      KR.toast('ล้มเหลว — ลองใหม่');
    });
  }

  /* stats poll ทุก 2 วิ (ผ่าน rpc แบบไม่บล็อก) */
  if (wv) {
    setInterval(function () {
      if (!document.getElementById('optBtn')) return;
      rpc('stats').then(function (s) { if (s && s.ready) fire('stats', s); }).catch(function () {});
    }, 2000);
  }

  if (wv) {
    /* ลากหน้าต่างจากแถบ header/sidebar + ดับเบิลคลิก maximize */
    const DRAG_SEL = '.topbar,.side,.brand,.title-block,.spacer,.bmeta,.hero,.panel,.ktop,.wrap';
    const NO_DRAG = 'input,button,select,textarea,label,a,.mi,.qa,.apply,.ghost,.optimize,.menu,.card,.glass,.term-b,.pill,.kb,.gen,.back,.sideout,.status';
    document.addEventListener('mousedown', function (ev) {
      if (ev.button !== 0) return;
      if (ev.target.closest && ev.target.closest(NO_DRAG)) return;
      if (ev.target.closest && ev.target.closest(DRAG_SEL)) rpc('window', { action: 'drag' });
    });
    document.addEventListener('dblclick', function (ev) {
      if (ev.target.closest && ev.target.closest(DRAG_SEL) && !ev.target.closest(NO_DRAG))
        rpc('window', { action: 'max' });
    });

    /* ปุ่มควบคุมหน้าต่างมุมขวาบน — ซ่อนไว้ ชี้มุมขวาบนค่อยลอยขึ้น */
    const bar = document.createElement('div');
    bar.style.cssText = 'position:fixed;top:10px;right:12px;z-index:9999;display:flex;gap:6px;' +
      'opacity:0;transition:opacity .25s ease;pointer-events:none;';
    [['\u2013', 'min'], ['\u25A1', 'max'], ['\u00D7', 'close']].forEach(function (pair) {
      const b = document.createElement('button');
      b.textContent = pair[0];
      b.style.cssText = 'width:30px;height:26px;border-radius:8px;border:1px solid rgba(255,255,255,.1);' +
        'background:rgba(255,255,255,.05);color:rgba(242,240,247,.75);font-size:13px;cursor:pointer;line-height:1;';
      b.onclick = function () { rpc('window', { action: pair[1] }); };
      bar.appendChild(b);
    });
    document.body.appendChild(bar);
    document.addEventListener('mousemove', function (ev) {
      const near = ev.clientX > window.innerWidth - 150 && ev.clientY < 64;
      bar.style.opacity = near ? '1' : '0';
      bar.style.pointerEvents = near ? 'auto' : 'none';
    });
  }

  /* งานลูกโซ่หลายขั้น — รันเป็น job เดียวบน C# → แถบ % ไหลต่อเนื่องตลอด ไม่รีเซ็ตระหว่างขั้น */
  function applyChain(steps) {
    return applyRpc('applyChain', { steps: steps });
  }
  window.KR = { rpc: rpc, on: on, toast: toast, apply: applyRpc, chain: applyChain, busyShow: busyShow, busyHide: busyHide, busyHideSoon: busyHideSoon, hosted: !!wv };

  /* ---- themed confirm modal (แทน confirm() ของเบราว์เซอร์) ---- */
  KR.confirm = function (msg, opts) {
    opts = opts || {};
    return new Promise(function (resolve) {
      const m = document.getElementById('mdl');
      if (!m) { resolve(window.confirm(msg)); return; }
      document.getElementById('mTitle').textContent = opts.title || 'ยืนยันการทำรายการ';
      document.getElementById('mMsg').textContent = msg;
      const yes = document.getElementById('mYes');
      yes.textContent = opts.yes || 'ยืนยัน';
      yes.className = 'mbtn ' + (opts.danger ? 'dangerok' : 'ok');
      m.classList.add('on');
      function done(v) {
        m.classList.remove('on');
        yes.removeEventListener('click', onY);
        document.getElementById('mNo').removeEventListener('click', onN);
        m.removeEventListener('click', onBg);
        resolve(v);
      }
      function onY() { done(true); }
      function onN() { done(false); }
      function onBg(e) { if (e.target === m) done(false); }
      yes.addEventListener('click', onY);
      document.getElementById('mNo').addEventListener('click', onN);
      m.addEventListener('click', onBg);
    });
  };
})();
/* ---------- LOGIN PAGE ---------- */
(function () {
  if (!window.KR || !window.KR.hosted) return;
  const KR = window.KR;
  const hwidVal = document.getElementById('hwidVal');
  if (!hwidVal) return; // ไม่ใช่หน้า login

  const keyInput = document.getElementById('pass');
  const btn = document.getElementById('btn'), bar = document.getElementById('bar'), btnText = document.getElementById('btnText');
  const statusText = document.getElementById('statusText');

  keyInput.addEventListener('input', function () {
    let v = keyInput.value.toUpperCase().replace(/[^A-Z0-9]/g, '').slice(0, 16);
    let out = v.match(/.{1,4}/g);
    keyInput.value = out ? out.join('-') : v;
  });

  KR.rpc('hwid').then(function (r) { hwidVal.textContent = r.hwid || '--------'; }).catch(function () {});
  KR.rpc('getSaved').then(function (r) { if (r.key) keyInput.value = r.key; }).catch(function () {});
  /* เวอร์ชันจริงจาก C# (AppVersion) → อัปเดตป้าย v1.0.x ในหน้า login อัตโนมัติ */
  KR.rpc('updateCheck').then(function (r) {
    if (r && r.local) document.querySelectorAll('.lver').forEach(function (el) { el.textContent = 'v' + r.local; });
  }).catch(function () {});

  document.getElementById('copyBtn').addEventListener('click', function () {
    KR.rpc('copy', { text: hwidVal.textContent }).then(function () { KR.toast('Copied HWID'); }).catch(function () {});
  });

  document.getElementById('lostLink').addEventListener('click', function (ev) {
    ev.preventDefault();
    KR.rpc('recover').then(function (r) {
      if (r.ok) { keyInput.value = r.key; KR.toast('Recovered: ' + r.key); }
      else KR.toast(r.msg || 'ไม่พบ key ของเครื่องนี้');
    }).catch(function () {});
  });

  /* นำเข้าไฟล์ key ที่แอดมินส่งมา (licenses.json) */
  const importLink = document.getElementById('importLink'), keyFile = document.getElementById('keyFile');
  if (importLink && keyFile) {
    importLink.addEventListener('click', function (ev) {
      ev.preventDefault();
      keyFile.value = '';
      keyFile.click();
    });
    keyFile.addEventListener('change', function () {
      const f = keyFile.files && keyFile.files[0];
      if (!f) return;
      const rd = new FileReader();
      rd.onload = function () {
        KR.rpc('importKey', { json: String(rd.result) }).then(function (r) {
          if (r.ok && r.count > 0) {
            if (r.key) keyInput.value = r.key;
            KR.toast('นำเข้า key สำเร็จ (' + r.count + ') — กด Activate ได้เลย');
          } else KR.toast(r.msg || 'ไฟล์ key ไม่ถูกต้อง');
        }).catch(function () {});
      };
      rd.readAsText(f);
    });
  }

  btn.addEventListener('click', function () {
    const key = keyInput.value.trim();
    if (key.length < 19) { KR.toast('กรอก key ให้ครบ 16 ตัว (XXXX-XXXX-XXXX-XXXX)'); return; }
    btn.classList.add('busy');
    btnText.textContent = 'Checking...';
    bar.style.width = '34%';
    statusText.textContent = 'Verifying license...';
    const rem = document.getElementById('remember');
    const remember = rem ? rem.checked : true;
    KR.rpc('activate', { key: key, remember: remember }).then(function (r) {
      if (r.ok) {
        bar.style.width = '100%';
        statusText.textContent = '✓ ' + (r.msg || 'Activated');
        statusText.style.color = '#7de3b8';
        KR.toast(r.msg || 'Activate สำเร็จ');
        setTimeout(function () { KR.rpc('enterDashboard').catch(function () {}); }, 700);
      } else {
        btn.classList.remove('busy');
        btnText.textContent = 'Activate';
        bar.style.width = '0%';
        statusText.textContent = '✕ ' + (r.msg || 'key ไม่ถูกต้อง');
        statusText.style.color = '#ff6b81';
        KR.toast(r.msg || 'key ไม่ถูกต้อง');
      }
    }).catch(function () {
      btn.classList.remove('busy');
      btnText.textContent = 'Activate';
      statusText.textContent = '✕ connection error';
    });
  });
})();
/* ---------- KEYGEN PAGE ---------- */
(function () {
  if (!window.KR || !window.KR.hosted) return;
  const KR = window.KR;
  const $ = function (id) { return document.getElementById(id); };
  const genBtn = document.getElementById('genBtn');
  if (!genBtn) return; // ไม่ใช่หน้า keygen

  const daysSel = document.getElementById('daysSel'), noteIn = document.getElementById('noteIn');
  const outBox = document.getElementById('outBox'), outKey = document.getElementById('outKey'), outSub = document.getElementById('outSub');
  const listEl = document.getElementById('keyList'), countEl = document.getElementById('keyCount');

  function esc(s) { return String(s == null ? '' : s).replace(/&/g, '&amp;').replace(/</g, '&lt;'); }

  function draw(items) {
    countEl.textContent = items.length + ' keys';
    if (!items.length) { listEl.innerHTML = '<div class="empty">ยังไม่มี key ในระบบ — เจน key แรกทางซ้ายได้เลย</div>'; return; }
    listEl.innerHTML = items.map(function (k) {
      const badge = k.revoked ? '<span class="bdg ban">ระงับการใช้งาน</span>'
        : (k.daysLeft < 0 ? '<span class="bdg exp">หมดอายุ</span>'
        : (!k.bound ? '<span class="bdg free">ว่าง · รอผูกเครื่องแรก</span>'
        : '<span class="bdg used">ผูกแล้ว · HWID ' + esc(k.hwid) + '</span>'));
      return '<div class="krow">' +
        '<div class="kmain2"><b>' + k.key + '</b>' + badge + (k.note ? '<small>' + esc(k.note) + '</small>' : '') + '</div>' +
        '<div class="kmeta">' + k.created + ' · ' + k.days + ' วัน' + (k.daysLeft >= 0 ? ' · เหลือ ' + k.daysLeft + ' วัน' : '') + '</div>' +
        '<div class="kact">' +
        '<button class="kb" data-act="file" data-k="' + k.key + '">ไฟล์</button>' +
        '<button class="kb" data-act="copy" data-k="' + k.key + '">คัดลอก</button>' +
        (SRV && !k.revoked ? '<button class="kb" data-act="extend" data-k="' + k.key + '">ต่ออายุ</button>' : '') +
        (k.bound && !k.revoked ? '<button class="kb warn" data-act="reset" data-k="' + k.key + '">รี HWID</button>' : '') +
        (SRV ? (k.revoked ? '<button class="kb" data-act="unban" data-k="' + k.key + '">ปลดระงับ</button>' : '<button class="kb danger" data-act="ban" data-k="' + k.key + '">ระงับ</button>') : '') +
        '<button class="kb danger" data-act="del" data-k="' + k.key + '">ลบ</button>' +
        '</div></div>';
    }).join('');
  }

  function load() {
    KR.rpc('listKeys').then(function (r) { if (r.ok) draw(r.items || []); }).catch(function () {});
  }

  let SRV = false, READY = false;
  KR.rpc('adminCheck').then(function (r) {
    SRV = !!r.server; READY = !!r.ready;
    if (!r.admin) {
      genBtn.disabled = true;
      genBtn.textContent = 'Admin only';
      daysSel.disabled = true;
      noteIn.disabled = true;
      listEl.innerHTML = '<div class="empty">🔒 หน้านี้ใช้ได้เฉพาะเครื่องแอดมินเท่านั้น</div>';
      return;
    }
    const h = document.getElementById('hintEl');
    if (SRV && !READY) {
      $('srvBox').style.display = 'block';
      if (h) h.innerHTML = '🌐 โหมดออนไลน์ — กรอกข้อมูล server ในการ์ดด้านบนก่อนใช้งาน (ครั้งเดียว)';
    } else if (h) {
      h.innerHTML = SRV
        ? '🌐 <b>โหมดออนไลน์</b> — เจน key แล้วส่ง <b>ตัว key</b> ให้ลูกค้าได้เลย (ไม่ต้องส่งไฟล์)<br>ลูกค้ากรอก key → ล็อค HWID ผ่าน server · รี HWID / ระงับ / ต่ออายุ ทำงานจริงทุกเครื่อง'
        : '📁 <b>โหมดไฟล์</b> — ยังไม่ได้เชื่อม server · ลูกค้านำเข้าไฟล์ key ผ่านหน้า Activate';
    }
    load();
  }).catch(function () { load(); });

  $('srvSave').addEventListener('click', async function () {
    const apiKey = $('srvKey').value.trim(), mail = $('srvMail').value.trim(), pass = $('srvPass').value;
    if (!apiKey || !mail || !pass) { $('srvErr').textContent = 'กรอกให้ครบทุกช่อง'; return; }
    $('srvSave').disabled = true;
    try {
      const r = await KR.rpc('serverSave', { apiKey: apiKey, email: mail, pass: pass });
      if (r.ok) {
        READY = true;
        $('srvBox').style.display = 'none';
        const h = document.getElementById('hintEl');
        if (h) h.innerHTML = '🌐 <b>โหมดออนไลน์พร้อมใช้</b>';
        KR.toast('เชื่อมต่อ server สำเร็จ');
        load();
      } else { $('srvErr').textContent = r.msg || 'บันทึกไม่สำเร็จ'; }
    } catch (e) { $('srvErr').textContent = e.message; }
    $('srvSave').disabled = false;
  });

  /* ---- Discord webhook (admin — แจ้งเตือน log เหตุการณ์สำคัญ) ---- */
  if ($('dcUrl')) {
    KR.rpc('discordGet').then(function (r) { if (r.ok) $('dcUrl').value = r.url || ''; }).catch(function () {});
    $('dcSave').addEventListener('click', async function () {
      const btn = $('dcSave'); btn.disabled = true;
      try {
        const r = await KR.rpc('discordSave', { url: $('dcUrl').value.trim() });
        const e = $('dcErr');
        e.style.color = r.ok ? '#3ba55d' : 'var(--red)';
        e.textContent = r.ok ? (r.msg || 'บันทึกแล้ว') : (r.msg || 'บันทึกไม่สำเร็จ');
        if (r.ok) KR.toast(r.msg || 'บันทึกแล้ว');
      } finally { btn.disabled = false; }
    });
    $('dcTest').addEventListener('click', async function () {
      const btn = $('dcTest'); const e = $('dcErr');
      btn.disabled = true; e.style.color = 'var(--red)'; e.textContent = 'กำลังส่ง...';
      try {
        const r = await KR.rpc('discordTest');
        e.style.color = r.ok ? '#3ba55d' : 'var(--red)';
        e.textContent = r.msg || '';
      } catch (ex) { e.textContent = 'ผิดพลาด: ' + ex.message; }
      finally { btn.disabled = false; }
    });
  }

  /* ---- เผยแพร่อัปเดตแอป (admin) ---- */
  if ($('pubVer')) {
    KR.rpc('updateCheck').then(function (d) {
      if (d && d.ok) { const el = $('pubLocal'); if (el) el.textContent = 'v' + (d.local || '?'); }
    }).catch(function () {});
    $('pubGo').addEventListener('click', async function () {
      const btn = $('pubGo'); const e = $('pubErr');
      btn.disabled = true; e.style.color = 'var(--red)'; e.textContent = 'กำลังเผยแพร่...';
      try {
        const r = await KR.rpc('updatePublish', { version: $('pubVer').value.trim(), url: $('pubUrl').value.trim(), sha256: $('pubSha256').value.trim(), notes: $('pubNotes').value.trim() });
        e.style.color = r.ok ? '#3ba55d' : 'var(--red)';
        e.textContent = r.msg || '';
        if (r.ok) KR.toast('เผยแพร่อัปเดตแล้ว ✓');
      } catch (ex) { e.textContent = 'ผิดพลาด: ' + ex.message; }
      finally { btn.disabled = false; }
    });
  }

  genBtn.addEventListener('click', function () {
    genBtn.classList.add('busy');
    genBtn.disabled = true;
    KR.rpc('genKey', { days: parseInt(daysSel.value, 10) || 30, note: noteIn.value })
      .then(function (r) {
        genBtn.classList.remove('busy');
        genBtn.disabled = false;
        if (r.ok) {
          outBox.classList.add('show');
          try { outBox.scrollIntoView({ block: 'nearest', behavior: 'smooth' }); } catch (e) { }
          outKey.textContent = r.key;
          outSub.textContent = 'อายุ ' + r.days + ' วัน · ยังไม่ผูกเครื่อง — คนแรกที่เอาไป Activate จะล็อค HWID ทันที';
          KR.toast('สร้าง key สำเร็จ');
          load();
        } else KR.toast(r.msg || 'สร้าง key ไม่สำเร็จ');
      })
      .catch(function () { genBtn.classList.remove('busy'); genBtn.disabled = false; });
  });

  document.getElementById('copyNew').addEventListener('click', function () {
    KR.rpc('copy', { text: outKey.textContent }).then(function () { KR.toast('Copied'); }).catch(function () {});
  });

  document.getElementById('backBtn').addEventListener('click', function () {
    KR.rpc('enterDashboard').catch(function () {});
  });

  listEl.addEventListener('click', function (ev) {
    const b = ev.target.closest('button.kb');
    if (!b) return;
    const k = b.dataset.k;
    if (b.dataset.act === 'file') {
      KR.rpc('exportKey', { key: k }).then(function (r) {
        if (r.ok) KR.toast('เซฟไฟล์ที่ Desktop แล้ว: ' + r.file);
        else KR.toast(r.msg || 'ส่งออกไม่สำเร็จ');
      }).catch(function () {});
    } else if (b.dataset.act === 'copy') {
      KR.rpc('copy', { text: k }).then(function () { KR.toast('Copied ' + k); }).catch(function () {});
    } else if (b.dataset.act === 'extend') {
      const d = prompt('ต่ออายุเป็นกี่วัน (นับจากวันนี้)?', '30');
      if (!d) return;
      const n = parseInt(d, 10);
      if (!n || n < 1) { KR.toast('จำนวนวันไม่ถูกต้อง'); return; }
      KR.rpc('extendKey', { key: k, days: n }).then(function (r) {
        if (r.ok) { KR.toast('ต่ออายุ ' + n + ' วันแล้ว'); load(); }
        else KR.toast(r.msg || 'ไม่สำเร็จ');
      }).catch(function () {});
    } else if (b.dataset.act === 'ban') {
      KR.confirm('ระงับ key นี้?\n' + k + '\n\nลูกค้าจะเข้าใช้ไม่ได้ทันทีที่เปิดแอปครั้งถัดไป', { title: 'ระงับ key', yes: 'ระงับ', danger: true })
        .then(function (ok) {
          if (!ok) return;
          KR.rpc('removeKey', { key: k }).then(function (r) {
            if (r.ok) { KR.toast('ระงับแล้ว'); load(); }
            else KR.toast(r.msg || 'ไม่สำเร็จ');
          }).catch(function () {});
        });
    } else if (b.dataset.act === 'unban') {
      KR.rpc('unbanKey', { key: k }).then(function (r) {
        if (r.ok) { KR.toast('ปลดระงับแล้ว'); load(); }
        else KR.toast(r.msg || 'ไม่สำเร็จ');
      }).catch(function () {});
    } else {
      KR.confirm('ลบ key นี้ถาวร?\n' + k + '\n\nถ้าเครื่องลูกค้าผูก key นี้ไว้แล้ว จะใช้งานไม่ได้ทันที', { title: 'ลบ key', yes: 'ลบ', danger: true })
        .then(function (ok) {
          if (!ok) return;
          KR.rpc('removeKey', { key: k }).then(function (r) {
            if (r.ok) { KR.toast('ลบ key แล้ว'); load(); }
          }).catch(function () {});
        });
    }
  });
})();
/* ---------- DASHBOARD PAGES (R9 layout) ---------- */
(function () {
  if (!window.KR || !window.KR.hosted) return;
  const KR = window.KR;
  const $ = function (id) { return document.getElementById(id); };
  if (!$('optBtn')) return; // ไม่ใช่หน้า dashboard

  /* ---- nav สลับหน้า ---- */
  const TITLES = {
    dash:   ['Dashboard', 'System health · quick launch · activity'],
    game:   ['Gaming Hub', 'Power plan · FiveM · STR · Services · Graphics'],
    ares:   ['Ares One-Click', 'Ares Store MAX optimizer — ปรับทุกอย่างในคลิกเดียว'],
    net:    ['Network Center', 'Auto adjust · QoS · R9 internet'],
    mem:    ['Memory & RAM', 'Live RAM usage'],
    hw:     ['Hardware Info', 'CPU · GPU · RAM · Motherboard — read live via WMI'],
    clean:  ['Junk Cleaner', 'Temp · Logs · Cache · Recycle Bin'],
    backup: ['Backup & Restore', 'Restore point · defaults rollback · folders'],
    logs:   ['Logs', 'Live activity stream'],
    guide:  ['วิธีใช้', 'คู่มือการใช้งาน — เริ่มต้น · เมนู · แก้ปัญหา'],
    about:  ['About', 'Version · license · folders']
  };
  let cur = 'dash';
  document.querySelectorAll('.mi').forEach(function (b) {
    b.addEventListener('click', function () {
      if (b.dataset.page === 'keygen') { KR.rpc('openKeygen').catch(function () {}); return; }
      document.querySelector('.mi.act')?.classList.remove('act');
      b.classList.add('act');
      cur = b.dataset.page;
      document.querySelectorAll('.page').forEach(function (p) { p.classList.toggle('act', p.id === 'pg-' + cur); });
      $('pgTitle').textContent = TITLES[cur][0];
      $('pgSub').textContent = TITLES[cur][1];
      if (cur === 'hw' && !mbLoaded) loadMb();
      document.querySelector('.scroll').scrollTop = 0;
      setTimeout(function () { KR.rpc('dbgScroll').catch(function () {}); }, 900);
    });
  });

  /* ---- logout (ปุ่มท้าย sidebar) ---- */
  const loBtn = $('btnLogout');
  if (loBtn) loBtn.addEventListener('click', function () {
    KR.confirm('ออกจากระบบ?\nระบบจะลืม key ที่จำไว้ในเครื่องนี้ และกลับไปหน้า Activate', { title: 'ออกจากระบบ', yes: 'Logout' }).then(function (ok) {
      if (ok) KR.rpc('logout').catch(function () {});
    });
  });
  setTimeout(function () { KR.rpc('dbgScroll').catch(function () {}); }, 1500);

  /* ---- ซ่อนเมนู Key Generator ถ้าไม่ใช่เครื่องแอดมิน ---- */
  KR.rpc('adminCheck').then(function (r) {
    if (!r.admin) {
      const k = document.querySelector('[data-page="keygen"]');
      if (k) k.style.display = 'none';
    }
  }).catch(function () {});

  /* ---- arcs + live stats → dashboard health + memory page ---- */
  const HC = 439.8, MC = 389.6;
  function arc(id, c, pct) {
    const el = $(id);
    if (!el) return;
    pct = Math.max(0, Math.min(100, pct));
    el.style.strokeDashoffset = c - c * pct / 100;
  }
  function put(id, v) { const el = $(id); if (el) el.textContent = v; }
  function bar(id, pct) {
    const el = $(id);
    if (!el) return;
    pct = Math.max(2, Math.min(100, pct));
    el.style.width = pct + '%';
  }
  KR.on('stats', function (d) {
    const cpu = d.cpu >= 0 ? d.cpu : null, ram = d.ramPct >= 0 ? d.ramPct : null,
          disk = d.diskPct >= 0 ? d.diskPct : null, ping = d.ping >= 0 ? d.ping : null;
    if (cpu !== null) { bar('bCpu', cpu); put('vbCpu', cpu + '%'); }
    put('cpuTemp', (cpu !== null ? cpu + '%' : '--') + (d.cpuTemp && d.cpuTemp !== '-' ? ' · ' + d.cpuTemp : ''));
    put('gpuTemp', (d.gpu != null && d.gpu >= 0 ? d.gpu + '%' : '--') + (d.gpuTemp && d.gpuTemp !== '-' ? ' · ' + d.gpuTemp : ''));
    if (ram !== null) {
      bar('bRam', ram);
      put('vbRam', ram + '%');
      put('memPct', ram + '%');
      arc('memArc', MC, ram);
      if (d.ramUsed) put('memSub', d.ramUsed + ' / ' + d.ramTotal + ' GB');
    }
    if (disk !== null) { bar('bDisk', disk); put('vbDisk', disk + '%'); }
    if (ping !== null) { bar('bPing', Math.max(0, 100 - ping * 2)); put('vbPing', ping + 'ms'); }
    let sum = 0, n = 0;
    if (cpu !== null) { sum += 100 - cpu * 0.9; n++; }
    if (ram !== null) { sum += 100 - ram * 0.8; n++; }
    if (disk !== null) { sum += 100 - disk * 0.5; n++; }
    if (ping !== null) { sum += Math.max(0, 100 - ping * 2); n++; }
    if (n) {
      const score = Math.round(sum / n);
      put('hcScore', String(score));
      put('wScore', score + '/100');
      arc('hcArc', HC, score);
    }
  });

  /* ---- logs + recent ---- */
  let logFilter = 'ALL';
  function esc(s) { return String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;'); }
  function drawLogs(items) {
    $('logCount').textContent = items.length + ' entries';
    const rows = items.filter(function (e) { return logFilter === 'ALL' || e.lvl === logFilter; });
    $('logList').innerHTML = rows.map(function (e) {
      return '<div class="lr"><span class="lt">' + e.t + '</span><span class="lv ' + e.lvl + '">[' + e.lvl + ']</span><span class="lmsg">' + esc(e.msg) + '</span></div>';
    }).join('') || '<div class="lr"><span class="lmsg">— ไม่มีรายการ —</span></div>';
    const rec = $('recentList');
    if (rec && cur === 'dash' && items.length) {
      rec.innerHTML = items.slice(-5).reverse().map(function (e) {
        return '<div class="ri"><span class="rd"></span><div><b>' + esc(e.msg.slice(0, 70)) + '</b><small>' + e.lvl + '</small></div><span class="rt">' + e.t + '</span></div>';
      }).join('');
    }
  }
  function pollLogs() {
    KR.rpc('logs').then(function (r) { if (r.ok) drawLogs(r.items); }).catch(function () {});
  }
  setInterval(pollLogs, 1500);
  pollLogs();
  document.querySelectorAll('.lf button').forEach(function (b) {
    b.addEventListener('click', function () {
      document.querySelector('.lf .act')?.classList.remove('act');
      b.classList.add('act');
      logFilter = b.dataset.lf;
      pollLogs();
    });
  });
  /* ---- header: Optimize / Restore + busy ---- */
  const optBtn = $('optBtn');
  function startOptimize() {
    if (document.body.dataset.busy === '1') return;
    KR.confirm('รันสคริปต์ชุดแนะนำทั้งหมด?\n(NIC 01 · Netsh/MTU 05 · QoS 02 · Services 06 · BCD 07 · R9 Plan 13 · FiveM+STR 09 · MMCSS · Nagle · DNS)\n\nระบบจะสำรอง Registry ก่อนโดยอัตโนมัติ', { title: 'KingR9 Optimize', yes: 'เริ่ม Optimize' }).then(function (ok) {
      if (!ok) return;
      optBtn.classList.remove('run'); void optBtn.offsetWidth; optBtn.classList.add('run');
      KR.rpc('optimize').catch(function () {});
    });
  }
  optBtn.addEventListener('click', startOptimize);
  $('restoreBtn').addEventListener('click', function () {
    if (document.body.dataset.busy === '1') return;
    KR.confirm('ถอน tweak ทั้งหมดกลับค่าเริ่มต้น Windows?\n(netsh reset · power plan defaults · BCD · ลบ QoS/priority · ถอน STR)\n\nแนะนำรีสตาร์ทเครื่องหลังทำ', { title: 'Restore Defaults', yes: 'Restore เลย', danger: true })
      .then(function (ok) { if (ok) KR.rpc('restoreAll').catch(function () {}); });
  });
  KR.on('busy', function (d) {
    document.body.dataset.busy = d.on ? '1' : '0';
    if (d.on) KR.busyShow('KingR9 Optimize', 'progress'); else KR.busyHideSoon(500);
  });
  /* ---- Optimize/Restore เสร็จ → ถามรีสตาร์ทด้วยหน้าต่างธีมของแอป (แทน MessageBox เทา ๆ ของ Windows) ---- */
  KR.on('askRestart', function (d) {
    const word = d.apply === 'res' ? 'คืนค่า (Restore)' : 'Optimize';
    KR.confirm(
      word + 'เสร็จแล้ว (' + (d.ok || 0) + ' สำเร็จ / ' + (d.fail || 0) + ' ล้มเหลว)\n\n' +
      'มี tweak ที่ต้องรีสตาร์ทเครื่องจึงจะมีผลสมบูรณ์ รีสตาร์ทเดี๋ยวนี้ไหม?',
      { title: 'รีสตาร์ทเครื่อง', yes: 'รีสตาร์ทเลย', danger: true }
    ).then(function (ok) {
      if (ok) {
        KR.toast('รีสตาร์ทใน 5 วินาที...');
        KR.rpc('restartNow').catch(function () {});
      } else {
        KR.toast('ไม่เป็นไร — รีสตาร์ทเองเมื่อไรก็ได้');
      }
    });
  });
  KR.on('applyProgress', function (d) {
    let lbl = optBtn.querySelector('.kr-lbl');
    if (!lbl) { lbl = document.createElement('span'); lbl.className = 'kr-lbl'; lbl.style.marginLeft = '6px'; optBtn.appendChild(lbl); }
    lbl.textContent = d.pct + '%';
  });
  KR.on('applyDone', function (d) {
    const lbl = optBtn.querySelector('.kr-lbl');
    if (lbl) setTimeout(function () { lbl.remove(); }, 900);
    KR.toast('Optimize เสร็จ — สำเร็จ ' + d.ok + ' · ล้มเหลว ' + d.fail);
  });

  /* ---- quick actions (dashboard) ---- */
  $('qaOpt').addEventListener('click', startOptimize);
  $('qaClean').addEventListener('click', function () {
    KR.apply('cleanJunk', null, 'Junk Cleaner')
      .then(function (r) { KR.toast('ล้างได้ ' + (r.total || 0).toFixed(2) + ' GB'); }).catch(function () {});
  });
  $('qaRp').addEventListener('click', function () {
    KR.confirm('สร้าง Windows Restore Point ตอนนี้?', { title: 'Create Restore Point', yes: 'สร้างเลย' }).then(function (ok) {
      if (!ok) return;
      KR.apply('sysTool', { action: 'restorept' }, 'Create Restore Point')
        .then(function () { KR.toast('สร้าง restore point แล้ว ✓'); }).catch(function () {});
    });
  });

  /* ---- memory page ---- */

  /* ---- junk cleaner page ---- */
  function renderJunk(r) {
    const box = $('clCats');
    if (!box) return;
    const total = r.total || 0;
    const rows = (r.cats || []).map(function (c) {
      const pct = total > 0 ? Math.max(3, Math.round((c.gb || 0) * 100 / total)) : 0;
      return '<div class="jrow"><div class="jn"><b>' + esc(c.name) + '</b><small>พร้อมล้าง</small></div><div class="jb"><i style="width:' + pct + '%"></i></div><span class="jv">' + (c.gb || 0).toFixed(2) + ' GB</span></div>';
    }).join('');
    box.innerHTML = rows || '<div class="jrow"><div class="jn"><b>ไม่พบไฟล์ขยะ</b><small>เก็บกวาดเรียบร้อยแล้ว</small></div><div class="jb"></div><span class="jv">0 GB</span></div>';
    put('clTotal', 'รวมไฟล์ขยะ ' + total.toFixed(2) + ' GB — กด Clean All เพื่อล้างด้วย script 12');
    put('clPill', total.toFixed(2) + ' GB');
    put('wJunk', total.toFixed(2) + ' GB');
  }
  $('clScan').addEventListener('click', function () {
    KR.toast('กำลังสแกนไฟล์ขยะ...');
    KR.rpc('getJunk').then(renderJunk).catch(function () {});
  });
  $('clClean').addEventListener('click', function () {
    KR.confirm('ล้างไฟล์ขยะทั้ง 4 หมวดถาวร?\n(Temp · Logs · Cache · Recycle Bin)', { title: 'Junk Cleaner', yes: 'ล้างเลย', danger: true }).then(function (ok) {
      if (!ok) return;
      KR.apply('cleanJunk', null, 'Junk Cleaner')
        .then(function (r) { KR.toast('ล้างได้ ' + (r.total || 0).toFixed(2) + ' GB'); renderJunk(r); }).catch(function () {});
    });
  });

  /* ---- backup & restore page ---- */
  $('bkPoint').addEventListener('click', function () {
    KR.confirm('สร้าง Windows Restore Point ตอนนี้?', { title: 'Create Restore Point', yes: 'สร้างเลย' }).then(function (ok) {
      if (!ok) return;
      KR.apply('sysTool', { action: 'restorept' }, 'Create Restore Point')
        .then(function () { KR.toast('สร้าง restore point แล้ว ✓'); }).catch(function () {});
    });
  });
  $('bkRestore').addEventListener('click', function () {
    KR.confirm('ROLLBACK ทุก tweak กลับค่าเริ่มต้น Windows?\n(12_System_Tools -Action defaults)\n\nแนะนำรีสตาร์ทเครื่องหลังทำ', { title: 'Rollback ทั้งระบบ', yes: 'Rollback เลย', danger: true })
      .then(function (ok) { if (ok) KR.rpc('restoreAll').catch(function () {}); });
  });
  $('bkFolder').addEventListener('click', function () { KR.rpc('openFolder', { which: 'backup' }).catch(function () {}); });

  /* สแกนขยะรอแรกเพื่อโชว์ที่ dashboard pill */
  KR.rpc('getJunk').then(renderJunk).catch(function () {});

  /* ---- powerplan page ---- */
  $('ppApply').addEventListener('click', function () {
    KR.apply('powerApply', {
      plan: $('ppPlan').checked, hib: $('ppHib').checked,
      boost: $('ppBoost').checked, delay: $('ppDelay').checked
    }, 'Power & BCD').catch(function () {});
  });
  $('ppDedupe').addEventListener('click', function () {
    KR.rpc('powerPlans').then(function (r) {
      if (!r || !r.ok) { KR.toast('ตรวจ Power Plan ไม่สำเร็จ'); return; }
      if (!r.dupCount) { KR.toast('ไม่พบ Power Plan ซ้ำ ✓ (มีทั้งหมด ' + (r.total || 0) + ' แผน)'); return; }
      const names = (r.dupNames || []).join('\n');
      KR.confirm('พบ Power Plan ซ้ำ ' + r.dupCount + ' ตัว:\n\n' + names + '\n\nลบส่วนเกินให้เหลืออันเดียวต่อชื่อ?\n(กลุ่มไหนมีแผนที่ใช้งานอยู่ จะเก็บแผนที่ใช้งานไว้)', { title: 'ลบ Power Plan ซ้ำ', yes: 'ลบเลย', danger: true }).then(function (ok2) {
        if (!ok2) return;
        KR.apply('powerPlanDedupe', null, 'ลบ Power Plan ซ้ำ').catch(function () {});
      });
    }).catch(function () { KR.toast('ตรวจ Power Plan ไม่สำเร็จ'); });
  });

  /* ---- fivem settings page ---- */
  $('fmApply').addEventListener('click', function () {
    KR.apply('fivemApply', {
      cache: $('fmCache').checked, prio: $('fmPrio').checked,
      cef: $('fmCef').checked, pkg: $('fmPkg').checked
    }, 'FiveM Tweaks').catch(function () {});
  });
  $('strApply').addEventListener('click', function () {
    KR.apply('strApply', { low: $('strSel').value === 'low' }, 'Timer Resolution').catch(function () {});
  });

  /* ---- Ares One-Click (รวมจากโปรเจกต์ Ares Store / JX Setting — optimizer.ps1 แบบ MAX) ---- */
  if ($('aresRun')) $('aresRun').addEventListener('click', function () {
    KR.confirm(
      'Ares One-Click จะปรับเครื่องแบบ MAX ทั้งหมด:\n\n' +
      '• ปิด telemetry / Copilot / Recall / Edge AI\n' +
      '• ลบ bloatware + ปิด services จำนวนมาก\n' +
      '• Game / GPU / Input / Network + QoS DSCP 46\n' +
      '• ลบ Power Plan เก่าของ Ares (ARESSTORE) — ใช้แผน KingR9 แทน\n' +
      '• จัดการ Timer / CPU / Memory / FS / Security\n' +
      '• ทำความสะอาด Temp / Cache / Logs\n\n' +
      'ใช้เวลา ~3-10 นาที' + ($('aresUpd').checked ? ' (ติ๊กอัปเดต Windows ไว้ — อาจนานมาก เพราะโหลด KB ~1-2 GB)' : '') +
      '\n\nเริ่มเลยไหม? (ห้ามปิดแอพกลางทาง)',
      { title: 'Ares One-Click Optimizer', yes: 'OPTIMIZE NOW', danger: true }
    ).then(function (ok) {
      if (!ok) return;
      KR.apply('aresRun', { update: $('aresUpd').checked }, 'Ares One-Click Optimizer').then(function (r) {
        if (r && r.ok) {
          KR.confirm('Ares เสร็จสมบูรณ์\n\nสำเร็จ ' + (r.okCount || 0) + ' · ล้มเหลว ' + (r.fail || 0) + ' · ข้าม ' + (r.skipped || 0) + '\n\nต้องรีสตาร์ทเครื่องจึงจะมีผลเต็มที่ — รีเดี๋ยวนี้ไหม?', { title: 'รีสตาร์ทเครื่อง', yes: 'รีสตาร์ทเลย' }).then(function (ok2) {
            if (ok2) { KR.toast('รีสตาร์ทใน 5 วินาที...'); KR.rpc('restartNow').catch(function () {}); }
            else KR.toast('ไม่เป็นไร — รีสตาร์ทเองเมื่อไรก็ได้');
          });
        }
      }).catch(function () {});
    });
  });
  if ($('aresRestart')) $('aresRestart').addEventListener('click', function () {
    KR.confirm('รีสตาร์ทเครื่องเดี๋ยวนี้? (บันทึกงานก่อน)', { title: 'RESTART WINDOWS', yes: 'รีสตาร์ทเลย', danger: true }).then(function (ok) {
      if (ok) { KR.toast('รีสตาร์ทใน 5 วินาที...'); KR.rpc('restartNow').catch(function () {}); }
    });
  });
  document.querySelectorAll('.str-opt').forEach(function (t) {
    t.addEventListener('click', function () {
      document.querySelectorAll('.str-opt').forEach(function (x) { x.classList.remove('act'); });
      t.classList.add('act');
      $('strSel').value = t.dataset.v;
    });
  });

  /* ---- tile/switch rows กดสลับ hidden checkbox (FiveM + Services) ---- */
  function bindTogs(sel) {
    document.querySelectorAll(sel).forEach(function (row) {
      const box = $(row.dataset.for);
      if (!box) return;
      const sync = function () {
        row.classList.toggle('on', box.checked);
        const tg = row.querySelector('.tg');
        if (tg) tg.textContent = box.checked ? 'ON' : 'OFF';
      };
      row.addEventListener('click', function () { box.checked = !box.checked; sync(); });
      sync();
    });
  }
  bindTogs('.ttile[data-for]');
  bindTogs('.swrow[data-for]');
  $('svApply').addEventListener('click', function () {
    KR.apply('svcApply', {
      netstack: $('svNet').checked, services: $('svSvc').checked, usb: $('svUsb').checked,
      latency: $('svLat').checked, bcd: $('svBcd').checked
    }, 'Services & OS Tweaks').catch(function () {});
  });

  /* ---- one-shot script pages ---- */
  $('qosApply').addEventListener('click', function () {
    KR.confirm('สร้าง QoS DSCP 46 (script 02) แล้วปิดท้ายด้วย Reset Adapter?\nเน็ตจะหลุด ~10-20 วิ ช่วงท้ายแล้วกลับมาเอง', { title: 'Apply QoS Settings', yes: 'Apply เลย' }).then(function (ok) {
      if (!ok) return;
      KR.chain([
        { type: 'setTweak', payload: { id: 'r9_qos', on: true } },
        { type: 'r9net', payload: { which: 'reset' } }
      ]).catch(function () {});
    });
  });
  $('gfxApply').addEventListener('click', function () {
    KR.apply('setTweak', { id: 'r9_gta5', on: true }, 'GTA5/FiveM Graphics').catch(function () {});
  });
  $('cfxApply').addEventListener('click', function () {
    KR.apply('setTweak', { id: 'r9_gta5', on: true }, 'CitizenFX.ini').catch(function () {});
  });
  $('netApply').addEventListener('click', function () {
    KR.confirm('Auto Adjust ทั้งหมด (NIC 01 + Netsh/MTU 05) แล้วปิดท้ายด้วย Reset Adapter?\nเน็ตจะหลุด ~10-20 วิ ช่วงท้ายแล้วกลับมาเอง', { title: 'Network Auto Adjust', yes: 'Apply เลย' }).then(function (ok) {
      if (!ok) return;
      KR.chain([
        { type: 'setTweak', payload: { id: 'r9_nic', on: true } },
        { type: 'setTweak', payload: { id: 'r9_netsh', on: true } },
        { type: 'r9net', payload: { which: 'reset' } }
      ]).catch(function () {});
    });
  });
  $('netRestore').addEventListener('click', function () {
    KR.confirm('คืนค่า network + system กลับค่าเริ่มต้นทั้งหมด?\n(12_System_Tools -Action defaults — แนะนำรีสตาร์ทหลังทำ)', { title: 'Network Restore', yes: 'คืนค่าเลย', danger: true })
      .then(function (ok) { if (ok) KR.rpc('restoreAll').catch(function () {}); });
  });

  /* ---- R9 internet ---- */
  function r9(which, busy) {
    KR.apply('r9net', { which: which }, 'R9 internet — ' + which).catch(function () {});
  }
  $('r9All').addEventListener('click', function () {
    KR.confirm('Apply R9 internet ทั้งชุด?\n(Registry 80 ค่า · Netsh · BCD · NIC · Bindings+DNS · Reset Adapter)\n\nช่วงท้ายจะ reset adapter — เน็ตจะหลุดแป๊บนึงแล้วกลับมาเอง\nแนะนำสร้าง Restore Point ก่อน (หน้า Backup)', { title: 'R9 internet — Apply All', yes: 'Apply เลย' }).then(function (ok) { if (ok) r9('all'); });
  });
  $('r9Registry').addEventListener('click', function () { r9('registry'); });
  $('r9Netsh').addEventListener('click', function () { r9('netsh'); });
  $('r9Bcd').addEventListener('click', function () {
    KR.confirm('เคลียร์ useplatformclock/tick + dynamic tick ON (ค่าปลอดภัย ไม่หน่วง)?\nต้องรีสตาร์ทเครื่องถึงมีผล', { title: 'BCD Tweaks', yes: 'ปรับเลย' }).then(function (ok) { if (ok) r9('bcd'); });
  });
  $('r9Nic').addEventListener('click', function () { r9('nic'); });
  $('r9Bind').addEventListener('click', function () {
    KR.confirm('ปิด Network Bindings + ตั้ง DNS เป็น Cloudflare (1.1.1.1)?\nอินเทอร์เน็ตอาจหลุดแป๊บนึงระหว่างทำ', { title: 'Bindings + DNS', yes: 'ทำเลย', danger: true }).then(function (ok) { if (ok) r9('bindings'); });
  });
  $('r9Restore').addEventListener('click', function () {
    KR.confirm('คืนค่า default ของชุด R9 internet?\n(autotuning normal · MTU default · DNS auto · เปิด bindings · ลบค่า Registry ที่เพิ่ม)', { title: 'R9 internet — Restore', yes: 'คืนค่าเลย', danger: true }).then(function (ok) { if (ok) r9('restore'); });
  });

  /* ---- motherboard page ---- */
  let mbLoaded = false, mbProd = '';
  function loadMb() {
    mbLoaded = true;
    KR.rpc('motherboard').then(function (r) {
      mbProd = r.product || '-';
      $('mbProd').textContent = mbProd;
      $('mbManu').textContent = 'Manufacturer: ' + (r.manufacturer || '-');
      if ($('hwCpu')) $('hwCpu').textContent = r.cpu || '-';
      if ($('hwGpu')) $('hwGpu').textContent = r.gpu || '-';
      if ($('hwRam')) $('hwRam').textContent = r.ram || '-';
    }).catch(function () { $('mbProd').textContent = '-'; });
  }
  $('mbSearch').addEventListener('click', function () {
    if (!mbProd) loadMb();
    const q = encodeURIComponent((mbProd || '') + ' motherboard specs');
    KR.rpc('openUrl', { url: 'https://www.google.com/search?q=' + q }).catch(function () {});
  });

  /* ---- about page ---- */
  $('abDiscord').addEventListener('click', function () {
    KR.rpc('openUrl', { url: 'https://discord.com' }).catch(function () {});
  });
  const abData = $('abData');
  if (abData) abData.addEventListener('click', function () { KR.rpc('openFolder', { which: '' }).catch(function () {}); });

  /* ---- state แรกจาก C# ---- */
  KR.on('state', function (d) {
    if (d.license) {
      const act = d.license.status === 'Active';
      const bs = $('brandState');
      if (bs) { bs.textContent = d.license.status; bs.style.color = act ? '' : 'var(--red)'; }
      const bv = $('brandVer'); if (bv) bv.textContent = 'v' + (d.version || '1.0');
      const bk = $('abKey'); if (bk) bk.textContent = d.license.key || '—';
      const bd = $('abDays'); if (bd) bd.textContent = 'เหลือ ' + (d.license.days || 0) + ' วัน · HWID: ' + (d.license.hwid || '-');
      const av = $('abVer'); if (av) av.textContent = 'Version ' + (d.version || '1.0') + ' · ' + (act ? 'Active License' : 'Inactive');
    }
    KR.toast('พร้อมใช้งาน ✓');
  });

  KR.rpc('dashboardReady').then(function (d) {
    /* ผล init จาก C# → ยิงเป็น event 'state' ให้ handler ด้านบนตั้งค่า About/license/เวอร์ชัน */
    if (window.__krDispatch) window.__krDispatch(Object.assign({ event: 'state' }, d || {}));
  }).catch(function () {});

  /* ---- auto update: ตรวจเวอร์ชันจาก server → banner → โหลดพร้อม % → รีสตาร์ทเอง ---- */
  let updDismissed = false;
  function checkUpdate() {
    if (!optBtn) return;
    KR.rpc('updateCheck').then(function (d) {
      if (d && d.ok && d.available && !updDismissed) updBanner(d);
    }).catch(function () {});
  }
  function updBanner(d) {
    let b = document.getElementById('updBanner');
    if (!b) {
      b = document.createElement('div');
      b.id = 'updBanner';
      b.style.cssText = 'position:fixed;top:58px;right:16px;z-index:600;max-width:320px;' +
        'background:linear-gradient(135deg,rgba(124,58,237,.4),rgba(37,99,235,.4));' +
        'border:1px solid rgba(255,255,255,.18);border-radius:14px;padding:12px 14px;' +
        'font-size:12.5px;backdrop-filter:blur(10px);box-shadow:0 8px 30px rgba(0,0,0,.45)';
      document.body.appendChild(b);
    }
    b.textContent = '';
    const t = document.createElement('b');
    t.textContent = '⬆️ มีเวอร์ชันใหม่ v' + (d.version || '') + ' (ตอนนี้ v' + (d.local || '') + ')';
    const s = document.createElement('small');
    s.style.cssText = 'display:block;opacity:.8;margin-top:3px';
    s.textContent = d.notes || '';
    const row = document.createElement('div');
    row.style.cssText = 'display:flex;gap:8px;margin-top:9px';
    const go = document.createElement('button');
    go.className = 'apply'; go.style.cssText = 'margin:0;padding:6px 14px;font-size:12px'; go.textContent = 'อัปเดตเลย';
    const later = document.createElement('button');
    later.className = 'ghost'; later.style.cssText = 'margin:0;padding:6px 14px;font-size:12px'; later.textContent = 'ภายหลัง';
    row.appendChild(go); row.appendChild(later);
    b.appendChild(t);
    if (d.notes) b.appendChild(s);
    b.appendChild(row);
    go.addEventListener('click', function () {
      b.textContent = '';
      const t2 = document.createElement('b');
      t2.textContent = '⬆️ กำลังอัปเดตเป็น v' + (d.version || '') + ' — โหลดเสร็จแอปจะรีสตาร์ทเอง';
      b.appendChild(t2);
      KR.apply('updateDownload', null, 'อัปเดตแอป v' + d.version).then(function (r) {
        if (r && r.ok === false) b.remove();
      });
    });
    later.addEventListener('click', function () { updDismissed = true; b.remove(); });
  }
  setTimeout(checkUpdate, 2500);
})();




