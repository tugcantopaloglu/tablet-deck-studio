package app.tabletdeck;

import javax.net.ssl.SSLSocket;
import javax.net.ssl.SSLSocketFactory;
import java.io.DataInputStream;
import java.io.DataOutputStream;
import java.io.IOException;
import java.net.InetSocketAddress;
import java.nio.ByteBuffer;
import java.nio.charset.CodingErrorAction;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.security.cert.X509Certificate;
import java.util.concurrent.Executors;
import java.util.concurrent.ScheduledExecutorService;
import java.util.concurrent.TimeUnit;

// Platform-only TLS transport. It never queues commands for reconnection or replays them.
public final class FastLink implements AutoCloseable {
    public static final class Settings {
        final String host,token,fingerprint;final SSLSocketFactory factory;final int port;
        public Settings(String host,int port,String token,String fingerprint,SSLSocketFactory factory){if(!token.matches("[A-F0-9]{64}")||!fingerprint.matches("[A-F0-9]{64}"))throw new IllegalArgumentException("Pair this tablet first.");this.host=host;this.port=port;this.token=token;this.fingerprint=fingerprint;this.factory=factory;}
    }
    public interface Listener {
        Settings settings() throws Exception;
        boolean packet(String json) throws Exception;
        void disconnected();
    }
    private final Listener listener;
    private final Object writeGate=new Object(),wake=new Object();
    private volatile boolean running=true,paused=false,ready=false;
    private volatile SSLSocket socket;
    private DataOutputStream output;
    private final Thread worker;
    private final ScheduledExecutorService heartbeat=Executors.newSingleThreadScheduledExecutor(r->{Thread t=new Thread(r,"TabletDeck link heartbeat");t.setDaemon(true);return t;});
    private volatile long pingAt;
    private volatile double roundTrip=-1;
    public FastLink(Listener listener){this.listener=listener;worker=new Thread(this::run,"TabletDeck fast TLS");worker.setDaemon(true);}
    public void start(){worker.start();heartbeat.scheduleAtFixedRate(()->{try{synchronized(writeGate){if(ready){pingAt=System.nanoTime();write("{\"id\":0,\"path\":\"/api/ping\",\"body\":{}}");}}}catch(IOException ignored){reset();}},2,8,TimeUnit.SECONDS);}
    public boolean connected(){return ready;}
    public double roundTripMs(){return roundTrip;}
    public void pong(){if(pingAt!=0)roundTrip=(System.nanoTime()-pingAt)/1000000.0;}
    public boolean send(String json) throws IOException {
        synchronized(writeGate){if(!ready)return false;write(json);return true;}
    }
    private void write(String json) throws IOException {
        byte[] bytes=json.getBytes(StandardCharsets.UTF_8);if(bytes.length>16384)throw new IOException("Control request exceeds limit.");
        byte[] frame=new byte[bytes.length+4];frame[0]=(byte)(bytes.length>>>24);frame[1]=(byte)(bytes.length>>>16);frame[2]=(byte)(bytes.length>>>8);frame[3]=(byte)bytes.length;System.arraycopy(bytes,0,frame,4,bytes.length);
        output.write(frame);output.flush();
    }
    private void run(){
        long retry=250;
        while(running){
            try {
                if(paused){waitFor(1000);continue;}
                Settings settings=listener.settings();if(settings==null){waitFor(1000);continue;}
                SSLSocket connection=(SSLSocket)settings.factory.createSocket();socket=connection;
                connection.setTcpNoDelay(true);connection.setKeepAlive(true);connection.connect(new InetSocketAddress(settings.host,settings.port),2500);connection.setSoTimeout(20000);connection.startHandshake();
                verify((X509Certificate)connection.getSession().getPeerCertificates()[0],settings.fingerprint);
                synchronized(writeGate){output=new DataOutputStream(connection.getOutputStream());write("{\"token\":\""+settings.token+"\"}");}
                DataInputStream input=new DataInputStream(connection.getInputStream());
                while(running&&!paused){int length=input.readInt();if(length<2||length>1024*1024)throw new IOException("Control response exceeds limit.");byte[] bytes=new byte[length];input.readFully(bytes);
                    String json=StandardCharsets.UTF_8.newDecoder().onMalformedInput(CodingErrorAction.REPORT).onUnmappableCharacter(CodingErrorAction.REPORT).decode(ByteBuffer.wrap(bytes)).toString();
                    if(listener.packet(json)){ready=true;retry=250;}
                }
            }catch(Exception ignored){/* HTTPS remains available; failures never replay controls. */}
            finally{ready=false;SSLSocket previous=socket;socket=null;if(previous!=null)try{previous.close();}catch(IOException ignored){}listener.disconnected();}
            if(running){waitFor(retry);retry=Math.min(5000,retry*2);}
        }
    }
    private static void verify(X509Certificate certificate,String expected) throws Exception {
        certificate.checkValidity();byte[] digest=MessageDigest.getInstance("SHA-256").digest(certificate.getEncoded());StringBuilder found=new StringBuilder();for(byte value:digest)found.append(String.format(java.util.Locale.ROOT,"%02X",value));
        if(!MessageDigest.isEqual(found.toString().getBytes(StandardCharsets.US_ASCII),expected.getBytes(StandardCharsets.US_ASCII)))throw new javax.net.ssl.SSLException("PC certificate pin mismatch.");
    }
    private void waitFor(long milliseconds){synchronized(wake){try{wake.wait(milliseconds);}catch(InterruptedException ignored){Thread.currentThread().interrupt();running=false;}}}
    public void reset(){ready=false;SSLSocket previous=socket;if(previous!=null)try{previous.close();}catch(IOException ignored){}synchronized(wake){wake.notifyAll();}}
    public void pause(){paused=true;reset();}
    public void resume(){paused=false;synchronized(wake){wake.notifyAll();}}
    @Override public void close(){running=false;heartbeat.shutdownNow();reset();}
}
