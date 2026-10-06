const target = new URL(process.env.BIZFLOW_E2E_API_URL);
if (target.protocol !== 'http:' || target.hostname !== '127.0.0.1') {
  throw new Error('The disposable test API must be bound to IPv4 loopback.');
}
module.exports = {
  '/api/**': { target: target.origin, secure: false },
};
