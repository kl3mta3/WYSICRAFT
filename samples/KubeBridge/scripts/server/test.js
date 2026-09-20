function on_click(event) {
  var clicks = Number(event.state.get('clicks') || 0) + 1;
  event.state.set('clicks', String(clicks));
  event.message('KubeJS received your WYSICRAFT click!');
  event.ui.setText('status', 'Server clicks: ' + clicks);
  event.runCommand('me tested the WYSICRAFT bridge');
  // Replace the command above with portal if your server provides it.
}
