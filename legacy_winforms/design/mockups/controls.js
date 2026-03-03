document.addEventListener('DOMContentLoaded', ()=>{
  const play = document.getElementById('play');
  const pause = document.getElementById('pause');
  const time = document.getElementById('time');
  let running = false; let t=15.27; const total=105;
  function update(){ time.textContent = `${t.toFixed(2)} / ${total.toFixed(2)} s`; }
  play.onclick = ()=>{ running=true; play.disabled=true; pause.disabled=false; tick(); }
  pause.onclick = ()=>{ running=false; play.disabled=false; pause.disabled=true; }
  function tick(){ if(!running) return; t+=0.2; if(t>total) t=total; update(); setTimeout(tick,200); }
  update();
});