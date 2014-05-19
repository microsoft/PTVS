from azure.servicebus import ServiceBusService
from azure.storage import CloudStorageAccount
import os

#
# The CloudStorageAccount provides factory methods for the queue, table, and
# blob services.
#
# See http://go.microsoft.com/fwlink/?linkid=246933 for Storage documentation.
#
STORAGE_ACCOUNT_NAME = '__paste_your_storage_account_name_here__'
STORAGE_ACCOUNT_KEY = '__paste_your_storage_key_here__'

if os.environ.get('EMULATED', '').lower() == 'true':
    # Running in the emulator, so use the development storage account
    storage_account = CloudStorageAccount(None, None)
else:
    storage_account = CloudStorageAccount(STORAGE_ACCOUNT_NAME, STORAGE_ACCOUNT_KEY)

blob_service = storage_account.create_blob_service()
table_service = storage_account.create_table_service()
queue_service = storage_account.create_queue_service()

#
# Service Bus is a messaging solution for applications. It sits between
# components of your applications and enables them to exchange messages in a
# loosely coupled way for improved scale and resiliency.
#
# See http://go.microsoft.com/fwlink/?linkid=246934 for Service Bus documentation.
#
SERVICE_BUS_NAMESPACE = '__paste_your_service_bus_namespace_here__'
SERVICE_BUS_KEY = '__paste_your_service_bus_key_here__'
bus_service = ServiceBusService(SERVICE_BUS_NAMESPACE, SERVICE_BUS_KEY, issuer='owner')


if __name__ == '__main__':
    while True:
        #
        # Write your worker process here.
        #
        # You will probably want to call a blocking function such as
        #    bus_service.receive_queue_message('queue name', timeout=seconds)
        # to avoid consuming 100% CPU time while your worker has no work.
        #
        # See below for more examples of using the services provided by Azure.
        #
        pass


#
# These examples can be deleted when you no longer need them.
#

def example_service_bus_queue():
    '''Creates a Service Bus Queue and waits for one message.'''
    
    bus_service.create_queue('example')
    
    while True:
        msg = bus_service.receive_queue_message('example')
        msg_text = msg.body
        
        print(msg_text)
        
        break

def example_blob():
    '''Creates a blob container, then adds and retrieves a text blob.'''
    
    blob_service.create_container('example')
    blob_service.put_block_blob_from_text('example', 'blob key', u'UTF-8 encoded text')
    
    value = blob_service.get_blob_to_text('example', 'blob key')
    print(value)

def example_table():
    '''Creates a table, adds two entities, and then retrieves one using a query.'''
    
    table_service.create_table('example')
    
    entry_1 = {
        'PartitionKey': 'examplePartition',
        'RowKey': '1',
        'name': 'pi',
        'number': 3.1415,
    }
    
    entry_2 = {
        'PartitionKey': 'examplePartition',
        'RowKey': '2',
        'name': 'e',
        'number': 2.7183,
    }
    
    table_service.insert_entity('example', entry_1)
    table_service.insert_entity('example', entry_2)
    entity = table_service.query_entities(
        'example',
        filter='number gt 3.0 and number lt 4.0',
        select='name'
    )
    
    print(entity[0].name)

def example_queue():
    '''Creates a queue, then adds and reads a single message.'''
    
    # Queues require messages to be base64 encoded, but version 0.8.0 of the
    # azure package does not automatically encode strings.
    from base64 import b64encode, b64decode
    from encodings import utf_8
    
    queue_service.create_queue('example')
    
    queue_service.put_message('example', b64encode(utf_8.encode('Queue message')[0]))
    
    message = queue_service.get_messages('example')[0]
    value = utf_8.decode(b64decode(message.message_text))[0]
    print(value.message_text)
